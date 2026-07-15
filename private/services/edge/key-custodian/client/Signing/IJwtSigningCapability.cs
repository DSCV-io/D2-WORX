// -----------------------------------------------------------------------
// <copyright file="IJwtSigningCapability.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Signing;

/// <summary>
/// The dedicated cluster-signing-root (<c>jwks-signing</c>) capability. Possession IS the
/// authority: this seam is registered ONLY in the JWT minter's (auth module's)
/// composition via <c>AddD2JwtSigningCapability()</c>, never in
/// <c>AddD2KeyCustodianClient()</c>. The general <c>IKeyCustodianApi.SignAsync</c>
/// surface can never sign <c>jwks-signing</c> for anyone — only a holder of this
/// capability can.
/// </summary>
/// <remarks>
/// The seam reuses the generated <see cref="SignInput"/> / <see cref="SignOutput"/>
/// transport DTOs (no hand-authored spec-mirror type). The impl gates on the
/// in-process-module plane (<c>AuthorizeMinterSigning</c>) before signing the active
/// <c>jwks-signing</c> key directly.
/// </remarks>
public interface IJwtSigningCapability
{
    /// <summary>
    /// Signs a JWT signing-input (<c>base64url(header).base64url(payload)</c>) with the
    /// active <c>jwks-signing</c> key. Authorized by possession of this capability plus an
    /// in-process-plane check.
    /// </summary>
    /// <param name="input">The signing input. The <c>keyDomain</c> field is ignored — the
    /// minter always targets the fixed cluster-signing root.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The signature + the kid that produced it, or a typed failure.</returns>
    ValueTask<D2Result<SignOutput>> SignJwtAsync(SignInput input, CancellationToken ct = default);
}
