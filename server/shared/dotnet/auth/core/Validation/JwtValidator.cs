// -----------------------------------------------------------------------
// <copyright file="JwtValidator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Validation;

using System.Diagnostics;
using System.Security.Claims;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Errors;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Context.Abstractions;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using JwtOutcome = D2.Shared.Auth.Telemetry.AuthTelemetryTags.JwtValidations.Outcome;

/// <summary>
/// JWT signature + standard-claim validator. Wraps
/// <see cref="JsonWebTokenHandler.ValidateTokenAsync(string, TokenValidationParameters)"/>,
/// plugs the live <see cref="IJwksProvider"/> snapshot into
/// <see cref="TokenValidationParameters.IssuerSigningKeys"/>, performs a
/// reactive-refresh-on-unknown-kid retry (cooldown-protected by the JWKS
/// provider), and hands the resulting <see cref="ClaimsPrincipal"/> to
/// <see cref="ClaimsToContextMapper"/>.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Layer scope</strong> — pure transformation
/// <c>string token → D2Result&lt;IRequestContext&gt;</c>. Side-effect-free
/// except for telemetry counters / log emissions / one possible JWKS refresh
/// on an unknown <c>kid</c>. The validator does NOT extract the bearer from
/// the <c>Authorization</c> header (that's the transport middleware's job),
/// does NOT enforce the session liveness check (transport middleware does that
/// AFTER the validator returns successfully), and does NOT enforce per-handler
/// scopes (BaseHandler's pipeline does that).
/// </para>
/// <para>
/// <strong>Algorithm pinning</strong> — the validator pins
/// <see cref="TokenValidationParameters.ValidAlgorithms"/> from
/// <see cref="JwtValidatorOptions.ValidAlgorithms"/> (default <c>["RS256"]</c>),
/// defending against <c>alg=none</c> + HMAC-with-public-key confusion attacks
/// at the standard validator surface — not via
/// <see cref="TokenValidationParameters.IssuerSigningKeyResolver"/> alone.
/// </para>
/// <para>
/// <strong>Reactive-refresh discipline</strong> — when the handler reports
/// signature key not found (typical kid-rotation gap), the validator forces
/// ONE JWKS refresh and retries ONCE. The
/// <see cref="D2.Shared.Auth.Jwks.HttpJwksProvider"/>'s Singleflight +
/// cooldown gates prevent multi-caller stampedes. After the single retry,
/// persistent failure surfaces as <see cref="AuthFailures.JwtKidNotFound"/>
/// (401, not 503 — the JWT itself is suspect, not the upstream JWKS).
/// </para>
/// <para>
/// <strong>PII discipline</strong> — JWT bytes, claim values, and <c>kid</c>
/// strings NEVER reach logs / span attributes / metric tags / exception
/// renderings. Logged outcomes are from a closed enumeration; exception
/// renderings flow through <see cref="D2.Shared.Utilities.Diagnostics.SanitizedExceptionRender"/>.
/// </para>
/// <para>
/// <strong>Thread-safety</strong> — registered as a singleton.
/// <see cref="JsonWebTokenHandler"/> is documented thread-safe and reusable;
/// we hold one static instance to avoid per-call allocation. Validator state
/// is otherwise immutable after construction.
/// </para>
/// </remarks>
internal sealed class JwtValidator
{
    // Reusable + thread-safe per Microsoft.IdentityModel docs — single static
    // readonly instance avoids per-call allocation across the entire process.
    private static readonly JsonWebTokenHandler sr_handler = new();

    private readonly IJwksProvider r_jwksProvider;
    private readonly ClaimsToContextMapper r_mapper;
    private readonly AuthOptions r_options;
    private readonly ILogger<JwtValidator> r_logger;

    /// <summary>Initializes a new instance of the <see cref="JwtValidator"/> class.</summary>
    /// <param name="jwksProvider">The JWKS provider for verify-key snapshots.</param>
    /// <param name="options">The auth options snapshot.</param>
    /// <param name="mapper">The claims-to-context mapper.</param>
    /// <param name="logger">The logger.</param>
    public JwtValidator(
        IJwksProvider jwksProvider,
        IOptions<AuthOptions> options,
        ClaimsToContextMapper mapper,
        ILogger<JwtValidator> logger)
    {
        ArgumentNullException.ThrowIfNull(jwksProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(mapper);
        ArgumentNullException.ThrowIfNull(logger);

        r_jwksProvider = jwksProvider;
        r_options = options.Value;
        r_mapper = mapper;
        r_logger = logger;
    }

    /// <summary>
    /// Validates a bearer JWT — signature + standard claims — and produces an
    /// <see cref="IRequestContext"/> populated from its claims.
    /// </summary>
    /// <param name="bearerToken">
    /// The raw JWT string. Pass WITHOUT any "<c>Bearer </c>" prefix —
    /// extraction from the transport <c>Authorization</c> header is the
    /// middleware's responsibility.
    /// </param>
    /// <param name="ct">Cancellation token honored by both validation and JWKS refresh.</param>
    /// <returns>
    /// <list type="bullet">
    ///   <item><see cref="D2Result{TData}"/>.Ok with the populated context on success.</item>
    ///   <item>One of <see cref="AuthFailures"/>'s <c>Jwt*</c> 401 helpers on
    ///     a structural / signature / claim / algorithm rejection.</item>
    ///   <item>
    ///     <see cref="AuthFailures.JwksUnavailable{T}"/> (503) when the upstream
    ///     JWKS endpoint is unreachable.
    ///   </item>
    /// </list>
    /// </returns>
    public async ValueTask<D2Result<IRequestContext>> ValidateAsync(
        string bearerToken,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // Pre-validation guard: bearer null/empty/whitespace short-circuits
        // before any JWKS work. The transport middleware MAY also reject
        // bearer-missing earlier; this is defense-in-depth.
        if (bearerToken.Falsey())
        {
            return RecordAndReturn(
                JwtOutcome.BEARER_MALFORMED, sw, BearerMalformed());
        }

        // Snapshot the keys once up-front — passed into TokenValidationParameters.
        // OperationCanceledException is allowed to propagate from any await — the
        // transport-layer host owns cancellation semantics; we never swallow.
        var snapshotResult = await r_jwksProvider.GetKeysAsync(ct).ConfigureAwait(false);
        if (snapshotResult.Success is false || snapshotResult.Data is null)
        {
            return RecordAndReturn(
                JwtOutcome.JWKS_UNAVAILABLE, sw, JwksUnavailable());
        }

        var snapshot = snapshotResult.Data;
        var validationParameters = BuildValidationParameters(snapshot);

        var first = await sr_handler
            .ValidateTokenAsync(bearerToken, validationParameters)
            .ConfigureAwait(false);

        // Reactive-refresh-on-unknown-kid: if the handler reports
        // SecurityTokenSignatureKeyNotFoundException, force a JWKS refresh
        // (cooldown-gated) and retry ONCE. No loop.
        if (first.IsValid is false
            && first.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            r_logger.JwtValidationReactiveRefreshTriggered();
            var refreshResult = await r_jwksProvider.RefreshAsync(ct).ConfigureAwait(false);
            if (refreshResult.Success is false)
            {
                return RecordAndReturn(
                    JwtOutcome.KID_NOT_FOUND, sw, JwtKidNotFound());
            }

            var refreshedSnapshot = await r_jwksProvider.GetKeysAsync(ct).ConfigureAwait(false);
            if (refreshedSnapshot.Success is false || refreshedSnapshot.Data is null)
            {
                return RecordAndReturn(
                    JwtOutcome.JWKS_UNAVAILABLE, sw, JwksUnavailable());
            }

            var retryParameters = BuildValidationParameters(refreshedSnapshot.Data);
            var second = await sr_handler
                .ValidateTokenAsync(bearerToken, retryParameters)
                .ConfigureAwait(false);
            return Finalize(second, sw, kidPathRetried: true);
        }

        return Finalize(first, sw, kidPathRetried: false);
    }

    private static D2Result<IRequestContext> BearerMalformed()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.BearerMalformed());

    private static D2Result<IRequestContext> JwksUnavailable()
        => AuthFailures.JwksUnavailable<IRequestContext>();

    private static D2Result<IRequestContext> JwtKidNotFound()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtKidNotFound());

    private static D2Result<IRequestContext> JwtSignatureInvalid()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtSignatureInvalid());

    private static D2Result<IRequestContext> JwtExpired()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtExpired());

    private static D2Result<IRequestContext> JwtNotYetValid()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtNotYetValid());

    private static D2Result<IRequestContext> JwtIssuerMismatch()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtIssuerMismatch());

    private static D2Result<IRequestContext> JwtAudienceMismatch()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtAudienceMismatch());

    private static D2Result<IRequestContext> JwtClaimMissing()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtClaimMissing());

    private static D2Result<IRequestContext> JwtActChainMalformed()
        => D2Result<IRequestContext>.BubbleFail(AuthFailures.JwtActChainMalformed());

    private static (string Outcome, D2Result<IRequestContext> Failure) Classify(Exception ex)
    {
        return ex switch
        {
            SecurityTokenExpiredException
                => (JwtOutcome.EXPIRED, JwtExpired()),
            SecurityTokenNotYetValidException
                => (JwtOutcome.NOT_YET_VALID, JwtNotYetValid()),
            SecurityTokenInvalidLifetimeException
                => (JwtOutcome.EXPIRED, JwtExpired()),
            SecurityTokenInvalidIssuerException
                => (JwtOutcome.ISSUER_MISMATCH, JwtIssuerMismatch()),
            SecurityTokenInvalidAudienceException
                => (JwtOutcome.AUDIENCE_MISMATCH, JwtAudienceMismatch()),
            SecurityTokenSignatureKeyNotFoundException
                => (JwtOutcome.KID_NOT_FOUND, JwtKidNotFound()),
            SecurityTokenInvalidSignatureException
                => (JwtOutcome.SIGNATURE_INVALID, JwtSignatureInvalid()),
            SecurityTokenInvalidAlgorithmException
                => (JwtOutcome.SIGNATURE_INVALID, JwtSignatureInvalid()),
            SecurityTokenNoExpirationException
                => (JwtOutcome.CLAIM_MISSING, JwtClaimMissing()),
            SecurityTokenMalformedException
                => (JwtOutcome.BEARER_MALFORMED, BearerMalformed()),
            ArgumentException
                => (JwtOutcome.BEARER_MALFORMED, BearerMalformed()),

            // Default: any other security-token rejection that didn't match a
            // specific subtype is treated as signature_invalid — closes the
            // outcome enumeration so unknown failures don't escape unclassified.
            _ => (JwtOutcome.SIGNATURE_INVALID, JwtSignatureInvalid()),
        };
    }

    private static string ClaimMissingErrorCode() => AuthErrorCodes.AUTH_JWT_CLAIM_MISSING;

    private static void RecordValidation(string outcome, double elapsedMs)
    {
        var outcomeTag = new KeyValuePair<string, object?>(
            AuthTelemetryTags.JwtValidations.TAG_OUTCOME, outcome);
        AuthTelemetry.SR_JwtValidations.Add(1, outcomeTag);
        AuthTelemetry.SR_JwtValidationDurationMs.Record(elapsedMs, outcomeTag);
    }

    private TokenValidationParameters BuildValidationParameters(JwksKeySetSnapshot snapshot)
    {
        // Resolve the snapshot's keys exactly — never fall back to "any key."
        // The IssuerSigningKeyResolver path is preferred over passing
        // IssuerSigningKeys directly because it gives us per-call kid lookup
        // logging hooks and avoids enumeration of every key for every call.
        var keys = snapshot.Keys.Values.ToList();

        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = r_options.Issuer?.ToString().TrimEnd('/'),
            ValidateAudience = true,
            ValidAudience = r_options.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = r_options.Validator.RequireExpirationTime,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = keys,
            ValidAlgorithms = r_options.Validator.ValidAlgorithms,
            ClockSkew = r_options.ClockSkew,
        };
    }

    private D2Result<IRequestContext> Finalize(
        TokenValidationResult result,
        Stopwatch sw,
        bool kidPathRetried)
    {
        if (result.IsValid)
        {
            // Build the principal and check post-validation invariants
            // (e.g. RequireSessionIdClaim) before mapping to the context.
            var identity = result.ClaimsIdentity ?? new ClaimsIdentity();
            var principal = new ClaimsPrincipal(identity);

            if (r_options.Validator.RequireSessionIdClaim
                && principal.FindFirst(JwtClaimTypes.SESSION_ID) is null)
            {
                return RecordAndReturn(JwtOutcome.CLAIM_MISSING, sw, JwtClaimMissing());
            }

            // The mapper's call into MutableRequestContext.FromClaims fans out
            // to ActorChainParser.ParseFromJsonString, which throws
            // MalformedActorChainException on RFC 8693 §2.1 violations
            // (non-object root, depth-limit blow-out, missing required claim,
            // invalid d2_kind, malformed JSON). Surface that as a 401 with
            // d2_error_code=AUTH_JWT_ACT_CHAIN_MALFORMED — the JWT's payload
            // is suspect, not a server fault. PII discipline: the exception
            // message includes only structural facts (depth, claim name,
            // ValueKind) — no claim values — so it stays out of the log
            // emission via the closed outcome enumeration.
            MutableRequestContext ctx;
            try
            {
                ctx = r_mapper.Map(principal);
            }
            catch (MalformedActorChainException)
            {
                return RecordAndReturn(JwtOutcome.ACT_CHAIN_MALFORMED, sw, JwtActChainMalformed());
            }

            sw.Stop();
            RecordValidation(JwtOutcome.SUCCESS, sw.Elapsed.TotalMilliseconds);
            return D2Result<IRequestContext>.Ok(ctx);
        }

        // The handler returned IsValid=false; classify by exception type.
        // Exception is non-null on a failed validation per
        // JsonWebTokenHandler's contract — defensive ?? handles a future API
        // change that returns IsValid=false without an exception.
        var ex = result.Exception
            ?? new SecurityTokenException("validation failed without exception");

        // After the kid-not-found retry path, a still-not-found result must
        // map to JwtKidNotFound — never mask it as "signature_invalid."
        if (kidPathRetried && ex is SecurityTokenSignatureKeyNotFoundException)
            return RecordAndReturn(JwtOutcome.KID_NOT_FOUND, sw, JwtKidNotFound());

        var (outcome, failure) = Classify(ex);
        return RecordAndReturn(outcome, sw, failure);
    }

    private D2Result<IRequestContext> RecordAndReturn(
        string outcome,
        Stopwatch sw,
        D2Result<IRequestContext> failure)
    {
        sw.Stop();
        var elapsedMs = sw.Elapsed.TotalMilliseconds;
        RecordValidation(outcome, elapsedMs);
        var errorCode = failure.ErrorCode.ToNullIfEmpty() ?? ClaimMissingErrorCode();
        r_logger.JwtValidationFailed(outcome, errorCode);
        return failure;
    }
}
