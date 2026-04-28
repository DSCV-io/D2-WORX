// -----------------------------------------------------------------------
// <copyright file="JwtFingerprintMiddleware.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.JwtAuth.Default;

using System.Net;
using D2.Shared.Handler;
using D2.Shared.Handler.Auth;
using D2.Shared.I18n;
using D2.Shared.RequestEnrichment.Default;
using D2.Shared.Result;
using D2.Shared.Utilities.Extensions;
using D2.Shared.Utilities.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware that validates the JWT <c>fp</c> (fingerprint) claim against the current
/// request's computed fingerprint. Prevents stolen JWT reuse from different clients.
/// </summary>
/// <remarks>
/// <para>Must run AFTER authentication middleware (needs <c>HttpContext.User</c> populated).</para>
/// <para>
/// Behavior:
/// <list type="bullet">
///   <item>No authenticated user → pass through (auth middleware handles this).</item>
///   <item>Trusted service (<see cref="IRequestContext.IsTrustedService"/>) → skip fingerprint validation entirely.</item>
///   <item>No <c>fp</c> claim (non-trusted) → 401 Unauthorized (fingerprint is required).</item>
///   <item><c>fp</c> claim matches computed fingerprint → pass through.</item>
///   <item><c>fp</c> claim does NOT match → 401 Unauthorized with D2Result error.</item>
/// </list>
/// </para>
/// </remarks>
public partial class JwtFingerprintMiddleware
{
    private readonly RequestDelegate r_next;
    private readonly ILogger<JwtFingerprintMiddleware> r_logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtFingerprintMiddleware"/> class.
    /// </summary>
    ///
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    public JwtFingerprintMiddleware(RequestDelegate next, ILogger<JwtFingerprintMiddleware> logger)
    {
        r_next = next;
        r_logger = logger;
    }

    /// <summary>
    /// Validates the JWT fingerprint claim against the request headers.
    /// </summary>
    ///
    /// <param name="context">The HTTP context.</param>
    ///
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (InfrastructurePaths.IsInfrastructure(context))
        {
            await r_next(context);
            return;
        }

        // Not authenticated → let auth middleware handle it.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await r_next(context);
            return;
        }

        // Trusted services skip fingerprint validation entirely.
        var mutableCtx = context.Features.Get<IRequestContext>() as MutableRequestContext;
        if (mutableCtx?.IsTrustedService == true)
        {
            SetAuthState(mutableCtx, context);
            await r_next(context);
            return;
        }

        // Extract the `fp` claim from the JWT.
        var fpClaim = context.User.FindFirst(JwtClaimTypes.FINGERPRINT)?.Value;

        // For non-trusted requests, fp claim is REQUIRED.
        if (fpClaim.Falsey())
        {
            LogMissingFingerprintClaim(r_logger, context.Request.Path.Value);

            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";

            var missingFpResponse = D2Result.Fail(
                [TK.Common.Errors.UNAUTHORIZED],
                HttpStatusCode.Unauthorized,
                errorCode: "MISSING_FINGERPRINT",
                traceId: context.TraceIdentifier);

            await context.Response.WriteAsJsonAsync(missingFpResponse, SerializerOptions.SR_Web, context.RequestAborted);
            return;
        }

        // Compute the expected fingerprint from request headers.
        var computed = JwtFingerprintValidator.ComputeFingerprint(context);

        // Compare (case-insensitive hex comparison).
        if (string.Equals(fpClaim, computed, StringComparison.OrdinalIgnoreCase))
        {
            SetAuthState(mutableCtx, context);
            await r_next(context);
            return;
        }

        // Mismatch — JWT is being used from a different client.
        LogFingerprintMismatch(r_logger, context.Request.Path.Value, Truncate(fpClaim ?? string.Empty), Truncate(computed));

        var response = D2Result.Fail(
            [TK.Common.Errors.UNAUTHORIZED],
            HttpStatusCode.Unauthorized,
            errorCode: "JWT_FINGERPRINT_MISMATCH",
            traceId: context.TraceIdentifier);

        context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(response, SerializerOptions.SR_Web, context.RequestAborted);
    }

    /// <summary>
    /// Populates authentication, identity, and organization fields on the
    /// <see cref="MutableRequestContext"/> from JWT claims. Called after successful
    /// fingerprint validation or for trusted services.
    /// </summary>
    private static void SetAuthState(MutableRequestContext? mutableCtx, HttpContext context)
    {
        if (mutableCtx is null)
        {
            return;
        }

        mutableCtx.IsAuthenticated = true;
        mutableCtx.UserIdRaw = context.User.FindFirst(JwtClaimTypes.SUB)?.Value;
        mutableCtx.Email = GetStringClaim(context, JwtClaimTypes.EMAIL);
        mutableCtx.Username = GetStringClaim(context, JwtClaimTypes.USERNAME);

        // Agent Organization.
        mutableCtx.AgentOrgId = GetGuidClaim(context, JwtClaimTypes.ORG_ID);
        mutableCtx.AgentOrgName = GetStringClaim(context, JwtClaimTypes.ORG_NAME);
        mutableCtx.AgentOrgType = GetOrgTypeClaim(context, JwtClaimTypes.ORG_TYPE);
        mutableCtx.AgentOrgRole = GetStringClaim(context, JwtClaimTypes.ROLE);

        // Org Emulation.
        mutableCtx.IsOrgEmulating = string.Equals(
            GetStringClaim(context, JwtClaimTypes.IS_EMULATING),
            "true",
            StringComparison.OrdinalIgnoreCase);

        // Target Organization — emulated org if emulating, otherwise agent org.
        if (mutableCtx.IsOrgEmulating == true)
        {
            mutableCtx.TargetOrgId = GetGuidClaim(context, JwtClaimTypes.EMULATED_ORG_ID) ?? mutableCtx.AgentOrgId;
            mutableCtx.TargetOrgName = GetStringClaim(context, JwtClaimTypes.EMULATED_ORG_NAME) ?? mutableCtx.AgentOrgName;
            mutableCtx.TargetOrgType = GetOrgTypeClaim(context, JwtClaimTypes.EMULATED_ORG_TYPE) ?? mutableCtx.AgentOrgType;
            mutableCtx.TargetOrgRole = "auditor";
        }
        else
        {
            mutableCtx.TargetOrgId = mutableCtx.AgentOrgId;
            mutableCtx.TargetOrgName = mutableCtx.AgentOrgName;
            mutableCtx.TargetOrgType = mutableCtx.AgentOrgType;
            mutableCtx.TargetOrgRole = mutableCtx.AgentOrgRole;
        }

        // User Impersonation.
        mutableCtx.ImpersonatedBy = GetGuidClaim(context, JwtClaimTypes.IMPERSONATED_BY);
        mutableCtx.ImpersonatingEmail = GetStringClaim(context, JwtClaimTypes.IMPERSONATING_EMAIL);
        mutableCtx.ImpersonatingUsername = GetStringClaim(context, JwtClaimTypes.IMPERSONATING_USERNAME);
        mutableCtx.IsUserImpersonating = string.Equals(
            GetStringClaim(context, JwtClaimTypes.IS_IMPERSONATING),
            "true",
            StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts a string claim value from the authenticated user principal.
    /// </summary>
    private static string? GetStringClaim(HttpContext ctx, string claimType)
    {
        var value = ctx.User.FindFirst(claimType)?.Value;
        return value.Falsey() ? null : value;
    }

    /// <summary>
    /// Extracts a Guid claim value from the authenticated user principal.
    /// </summary>
    private static Guid? GetGuidClaim(HttpContext ctx, string claimType)
    {
        var value = ctx.User.FindFirst(claimType)?.Value;
        if (value.Falsey())
        {
            return null;
        }

        return Guid.TryParse(value, out var guid) ? guid : null;
    }

    /// <summary>
    /// Extracts an OrgType claim value from the authenticated user principal.
    /// </summary>
    private static OrgType? GetOrgTypeClaim(HttpContext ctx, string claimType)
    {
        var value = ctx.User.FindFirst(claimType)?.Value;
        if (value.Falsey())
        {
            return null;
        }

        return value!.ToLowerInvariant() switch
        {
            OrgTypeValues.ADMIN => OrgType.Admin,
            OrgTypeValues.SUPPORT => OrgType.Support,
            OrgTypeValues.AFFILIATE => OrgType.Affiliate,
            OrgTypeValues.CUSTOMER => OrgType.Customer,
            OrgTypeValues.THIRD_PARTY => OrgType.ThirdParty,
            _ => Enum.TryParse<OrgType>(value, ignoreCase: true, out var parsed) ? parsed : null,
        };
    }

    /// <summary>
    /// Safely truncates a fingerprint hash for logging (avoids IndexOutOfRangeException on short values).
    /// </summary>
    private static string Truncate(string value)
    {
        const int prefix_length = 8;
        return value.Length > prefix_length
            ? value[..prefix_length] + "..."
            : value + "...";
    }

    /// <summary>
    /// Logs that a JWT is missing the required fingerprint claim.
    /// </summary>
    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "JWT missing required fingerprint claim for {Path}")]
    private static partial void LogMissingFingerprintClaim(ILogger logger, string? path);

    /// <summary>
    /// Logs a JWT fingerprint mismatch between expected and actual values.
    /// </summary>
    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "JWT fingerprint mismatch for {Path}. Expected: {Expected}, Got: {Got}")]
    private static partial void LogFingerprintMismatch(ILogger logger, string? path, string expected, string got);
}
