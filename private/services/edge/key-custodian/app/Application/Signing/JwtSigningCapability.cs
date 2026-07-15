// -----------------------------------------------------------------------
// <copyright file="JwtSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Signing;

using D2.Edge.KeyCustodian.Client.Signing;
using D2.Shared.Context.Abstractions;
using Microsoft.Extensions.Logging;

/// <summary>
/// The dedicated cluster-signing-root (<c>jwks-signing</c>) capability impl. POSSESSION is
/// the authority (this seam is registered ONLY in the JWT minter's composition via
/// <see cref="JwtSigningCapabilityServiceCollectionExtensions"/>, never in
/// <c>AddD2KeyCustodianClient()</c>). The general <c>IKeyCustodianApi.SignAsync</c> surface
/// can never sign <c>jwks-signing</c>. This impl asserts the in-process plane via
/// <see cref="WorkloadCapabilityAuthority.AuthorizeMinterSigning"/> (requires
/// <c>Origin == InProcessModule</c>), then signs the active <c>jwks-signing</c> key
/// directly via the same load + decrypt + zero + <see cref="RsaSigning"/> core
/// (<see cref="KeyDomainSigner"/>) the general handler uses.
/// </summary>
internal sealed class JwtSigningCapability(
    IKeyCustodianDbContext db,
    [FromKeyedServices(KeyCustodianRootKey.ROOT_SERVICE_KEY)] IPayloadCrypto rootCrypto,
    IRequestContext requestContext,
    ILogger<JwtSigningCapability> logger)
    : IJwtSigningCapability
{
    /// <inheritdoc/>
    public async ValueTask<D2Result<SignOutput>> SignJwtAsync(
        SignInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Possession got us here; this asserts the in-process plane (the minter rule). The
        // capability does NOT route through AuthorizeSigning (whose general-surface arm
        // categorically rejects jwks-signing).
        var authResult = WorkloadCapabilityAuthority.AuthorizeMinterSigning(requestContext.Origin);

        if (authResult.Failed)
            return DenyWithTelemetry(authResult);

        // Sign the jwks-signing active key directly via the shared signing core. The
        // caller-supplied keyDomain is ignored — the minter always targets the root.
        return await KeyDomainSigner
            .SignActiveKeyAsync(db, rootCrypto, KeyDomain.JwksSigning, input.SigningInput, ct)
            .ConfigureAwait(false);
    }

    private D2Result<SignOutput> DenyWithTelemetry(D2Result authResult)
    {
        // A minter deny is the highest-stakes authority event (a bug invoking from the
        // wrong plane, or a genuine attempt on the cluster-signing root) and must never be
        // silent. Map the minter rule's typed failure to a bounded reason tag from the
        // KeyCustodianMetrics.AuthorityRejections named-constant closed set: the minter
        // denies on exactly two arms — an unestablished origin, or an established origin that
        // is not the in-process-module plane (Forbidden). The general-surface cross-process
        // counter is NOT fired here; this is the dedicated in-process minter capability, not
        // the general signing surface that counter tracks.
        var reason = authResult.ErrorCode switch
        {
            KeyCustodianErrorCodes.KEYCUSTODIAN_REQUEST_ORIGIN_UNESTABLISHED
                => KeyCustodianMetrics.AuthorityRejections.Reason.ORIGIN_UNESTABLISHED,

            // Forbidden — wrong plane (not the in-process-module).
            _ => KeyCustodianMetrics.AuthorityRejections.Reason.NOT_IN_PROCESS,
        };

        KeyCustodianLog.AuthorityRejected(
            logger,
            KeyCustodianMetrics.AuthorityRejections.Workload.IN_PROCESS_MINTER,
            KeyCustodianMetrics.AuthorityRejections.Capability.SIGN,
            KeyDomain.JWKS_SIGNING);

        KeyCustodianMetrics.SR_AuthorityRejectionsTotal.Add(
            1,
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_CAPABILITY,
                KeyCustodianMetrics.AuthorityRejections.Capability.SIGN),
            new KeyValuePair<string, object?>(
                KeyCustodianMetrics.AuthorityRejections.TAG_REASON, reason));

        return D2Result<SignOutput>.BubbleFail(authResult);
    }
}
