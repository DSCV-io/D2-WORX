// -----------------------------------------------------------------------
// <copyright file="KeyringAadProjection.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Domain.Rules;

/// <summary>
/// The single KC-side computation of a payload domain's AEAD additional-authenticated-data
/// (AAD): the UTF-8 bytes of <c>"d2/&lt;domain&gt;"</c>. The keyring fetch surface returns
/// this on the wire so a consumer assembles a <c>PayloadCryptoKeyring</c> bound to the
/// exact same context KeyCustodian computed.
/// </summary>
/// <remarks>
/// <para>
/// <b>Pure no-IO domain rule (§9.36a).</b> No DB, no options, no logging, no clock — a
/// deterministic function of the domain value.
/// </para>
/// <para>
/// <b>⚠ Stable-per-domain-for-life.</b> The AAD is authenticated (not secret) context
/// bound into EVERY (en|de)crypt operation for the domain. Changing the projection —
/// the prefix, the separator, or the encoding — would make every value already
/// encrypted under the old AAD FAIL to decrypt (an AEAD tag mismatch). This convention
/// is frozen; the byte layout is pinned by a freeze test that carries the literal bytes
/// per payload domain, so any edit here breaks that test loudly.
/// </para>
/// </remarks>
public static class KeyringAadProjection
{
    /// <summary>
    /// The immutable domain-AAD prefix. <c>"d2/" + domain.Value</c> is the frozen shape.
    /// </summary>
    private const string _AAD_PREFIX = "d2/";

    /// <summary>
    /// Computes the AEAD additional-authenticated-data for <paramref name="domain"/>: the
    /// UTF-8 bytes of <c>"d2/&lt;domain&gt;"</c>. A fresh array on every call (the caller
    /// owns it — it crosses the wire and is copied into a keyring).
    /// </summary>
    /// <param name="domain">The payload-encryption key domain.</param>
    /// <returns>The UTF-8 AAD bytes (non-empty — a domain value is never empty).</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="domain"/> is null.
    /// </exception>
    public static byte[] For(KeyDomain domain)
    {
        ArgumentNullException.ThrowIfNull(domain);

        return Encoding.UTF8.GetBytes(_AAD_PREFIX + domain.Value);
    }
}
