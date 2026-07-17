// -----------------------------------------------------------------------
// <copyright file="SealingOutputMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

using System.Linq;
using System.Security.Cryptography;
using DcsvIo.D2.Encryption;
using ProtoPrivateOutput =
    global::D2.Services.Protos.KeyCustodian.V2Alpha.GetOrLazyProvisionOwnSealPrivateKeyOutput;
using ProtoPublicOutput =
    global::D2.Services.Protos.KeyCustodian.V2Alpha.GetOrLazyProvisionSealPublicKeyOutput;

/// <summary>
/// The single boundary mapper where the KeyCustodian seal-keyring wire shape becomes
/// a <see cref="RecipientPublicKeyring"/> / <see cref="RecipientPrivateKeyring"/> crypto
/// primitive — the sealed sibling of <c>KeyringOutputMapper</c>. Both fetch sources (the
/// cross-process gRPC reply and the in-process leaf DTO) funnel through the same
/// leaf-DTO to keyring conversion, so the defensive invariant handling and the
/// <c>[RedactData]</c>-annotated DTO intermediary are shared (uppermost-node rule). The
/// private conversion zeroes its intermediate PKCS#8 copies once the keyring (which copies
/// them into its own zeroize-on-dispose buffers) is built.
/// </summary>
internal static class SealingOutputMapper
{
    /// <param name="output">The KeyCustodian leaf public seal-keyring DTO.</param>
    extension(GetOrLazyProvisionSealPublicKeyOutput output)
    {
        /// <summary>
        /// Builds a <see cref="RecipientPublicKeyring"/> from the leaf DTO. A payload that
        /// violates a keyring invariant (empty entries, active kid absent, a non-P-256 SPKI)
        /// surfaces as a typed failure — never an unhandled ctor throw.
        /// </summary>
        /// <param name="recipientServiceId">The recipient service the keys seal to.</param>
        internal D2Result<RecipientPublicKeyring> ToRecipientPublicKeyring(
            string recipientServiceId)
        {
            var keys = new Dictionary<string, byte[]>(output.Entries.Count, StringComparer.Ordinal);
            foreach (var entry in output.Entries)
                keys[entry.Kid] = entry.PublicSpki;

            try
            {
                return D2Result<RecipientPublicKeyring>.Ok(
                    new RecipientPublicKeyring(recipientServiceId, output.ActiveKid, keys));
            }
            catch (ArgumentException)
            {
                // Defensive boundary: KeyCustodian returned a public seal keyring that
                // violates a RecipientPublicKeyring invariant. Typed service-contract failure.
                return D2Result<RecipientPublicKeyring>.ServiceUnavailable();
            }
        }
    }

    /// <param name="output">The KeyCustodian leaf private seal-keyring DTO.</param>
    extension(GetOrLazyProvisionOwnSealPrivateKeyOutput output)
    {
        /// <summary>
        /// Builds a <see cref="RecipientPrivateKeyring"/> from the leaf DTO, then zeroes the
        /// intermediate PKCS#8 copies (the keyring holds its own defensive, zeroize-on-dispose
        /// copies). A payload that violates a keyring invariant surfaces as a typed failure.
        /// </summary>
        /// <param name="ownServiceId">The caller's own service id (anchors the AEAD binding).</param>
        internal D2Result<RecipientPrivateKeyring> ToRecipientPrivateKeyring(string ownServiceId)
        {
            var keys = new Dictionary<string, byte[]>(output.Entries.Count, StringComparer.Ordinal);
            foreach (var entry in output.Entries)
                keys[entry.Kid] = entry.PrivatePkcs8;

            try
            {
                return D2Result<RecipientPrivateKeyring>.Ok(
                    new RecipientPrivateKeyring(ownServiceId, keys));
            }
            catch (ArgumentException)
            {
                // Defensive boundary: KeyCustodian returned a private seal keyring that
                // violates a RecipientPrivateKeyring invariant. Typed service-contract failure.
                return D2Result<RecipientPrivateKeyring>.ServiceUnavailable();
            }
            finally
            {
                // Zeroize the intermediate PKCS#8 copies — the keyring copied them into its
                // own buffers (or the ctor threw before doing so); either way, no plaintext
                // private key lingers in this transient dictionary.
                foreach (var bytes in keys.Values)
                    CryptographicOperations.ZeroMemory(bytes);
            }
        }
    }

    /// <param name="proto">The KeyCustodian gRPC public seal-keyring reply body.</param>
    extension(ProtoPublicOutput proto)
    {
        /// <summary>
        /// Converts the gRPC public-keyring reply body into the leaf DTO so both sources
        /// share one keyring builder. Public key material is wire-public — no redaction needed.
        /// </summary>
        internal GetOrLazyProvisionSealPublicKeyOutput ToClientsOutput()
            => new(
                proto.ActiveKid,
                proto.Entries
                    .Select(static e => new SealPublicEntry(e.Kid, e.PublicSpki.ToByteArray()))
                    .ToList());
    }

    /// <param name="proto">The KeyCustodian gRPC private seal-keyring reply body.</param>
    extension(ProtoPrivateOutput proto)
    {
        /// <summary>
        /// Converts the gRPC private-keyring reply body into the
        /// <c>[RedactData(SecretInformation)]</c>-annotated leaf DTO so the wire proto is
        /// immediately superseded by a redacted shape (private key bytes can never render in
        /// a structured log) and both sources share one keyring builder.
        /// </summary>
        internal GetOrLazyProvisionOwnSealPrivateKeyOutput ToClientsOutput()
            => new(
                proto.ActiveKid,
                proto.Entries
                    .Select(static e => new SealPrivateEntry(e.Kid, e.PrivatePkcs8.ToByteArray()))
                    .ToList());
    }

    /// <summary>
    /// Maps a leaf-DTO public-keyring fetch outcome to a keyring result: propagates a failure
    /// verbatim, treats a success carrying no data as a service-contract failure, and otherwise
    /// builds the keyring. Takes the envelope + data separately so both sources' differing data
    /// nullability annotations converge on one code path.
    /// </summary>
    /// <param name="envelope">The fetch result envelope (success/failure + status/code).</param>
    /// <param name="data">The leaf public seal-keyring DTO, if the fetch produced one.</param>
    /// <param name="recipientServiceId">The recipient service the keys seal to.</param>
    /// <returns>A public seal-keyring result.</returns>
    internal static D2Result<RecipientPublicKeyring> ToPublicKeyringResult(
        D2Result envelope, GetOrLazyProvisionSealPublicKeyOutput? data, string recipientServiceId)
    {
        if (envelope.Failed)
            return D2Result<RecipientPublicKeyring>.BubbleFail(envelope);

        if (data is null)
            return D2Result<RecipientPublicKeyring>.ServiceUnavailable();

        return data.ToRecipientPublicKeyring(recipientServiceId);
    }

    /// <summary>
    /// Maps a leaf-DTO private-keyring fetch outcome to a keyring result (the private sibling
    /// of <see cref="ToPublicKeyringResult"/>).
    /// </summary>
    /// <param name="envelope">The fetch result envelope (success/failure + status/code).</param>
    /// <param name="data">The leaf private seal-keyring DTO, if the fetch produced one.</param>
    /// <param name="ownServiceId">The caller's own service id (anchors the AEAD binding).</param>
    /// <returns>A private seal-keyring result.</returns>
    internal static D2Result<RecipientPrivateKeyring> ToPrivateKeyringResult(
        D2Result envelope, GetOrLazyProvisionOwnSealPrivateKeyOutput? data, string ownServiceId)
    {
        if (envelope.Failed)
            return D2Result<RecipientPrivateKeyring>.BubbleFail(envelope);

        if (data is null)
            return D2Result<RecipientPrivateKeyring>.ServiceUnavailable();

        return data.ToRecipientPrivateKeyring(ownServiceId);
    }
}
