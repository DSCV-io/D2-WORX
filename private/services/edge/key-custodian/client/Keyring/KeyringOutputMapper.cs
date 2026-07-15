// -----------------------------------------------------------------------
// <copyright file="KeyringOutputMapper.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using System.Linq;
using D2.Shared.Encryption;
using ProtoGetKeyringOutput = D2.Services.Protos.KeyCustodian.V2Alpha.GetKeyringOutput;

/// <summary>
/// The single boundary mapper where the KeyCustodian keyring wire shape becomes the
/// <see cref="PayloadCryptoKeyring"/> crypto primitive. Both fetch sources (the
/// cross-process gRPC reply and the in-process leaf DTO) funnel through the same
/// leaf-DTO to keyring conversion, so the defensive invariant handling and the
/// <c>[RedactData]</c>-annotated DTO intermediary are shared (uppermost-node rule).
/// </summary>
internal static class KeyringOutputMapper
{
    /// <param name="output">The KeyCustodian leaf keyring DTO.</param>
    extension(GetKeyringOutput output)
    {
        /// <summary>
        /// Builds a <see cref="PayloadCryptoKeyring"/> from the leaf DTO. A payload that
        /// violates a keyring invariant (empty AAD, wrong key length, active kid absent
        /// from the entries) surfaces as a typed failure — never an unhandled ctor throw.
        /// </summary>
        internal D2Result<PayloadCryptoKeyring> ToPayloadCryptoKeyring()
        {
            var keys = new Dictionary<string, byte[]>(output.Entries.Count, StringComparer.Ordinal);
            foreach (var entry in output.Entries)
                keys[entry.Kid] = entry.KeyBytes;

            try
            {
                return D2Result<PayloadCryptoKeyring>.Ok(
                    new PayloadCryptoKeyring(output.ActiveKid, keys, output.AadContext));
            }
            catch (ArgumentException)
            {
                // Defensive boundary: KeyCustodian returned a keyring that violates a
                // PayloadCryptoKeyring invariant. Surface a typed service-contract failure.
                return D2Result<PayloadCryptoKeyring>.ServiceUnavailable();
            }
        }
    }

    /// <param name="proto">The KeyCustodian gRPC keyring reply body.</param>
    extension(ProtoGetKeyringOutput proto)
    {
        /// <summary>
        /// Converts the gRPC reply body into the <c>[RedactData]</c>-annotated leaf DTO
        /// so the wire proto is immediately superseded by a redacted shape (key bytes can
        /// never render in a structured log) and both sources share one keyring builder.
        /// </summary>
        internal GetKeyringOutput ToClientsOutput()
            => new GetKeyringOutput(
                proto.ActiveKid,
                proto.Entries
                    .Select(static e => new KeyringEntry(e.Kid, e.KeyBytes.ToByteArray()))
                    .ToList(),
                proto.AadContext.ToByteArray());
    }

    /// <summary>
    /// Maps a leaf-DTO fetch outcome to a keyring result: propagates a failure verbatim,
    /// treats a success carrying no data as a service-contract failure, and otherwise builds
    /// the keyring. Takes the envelope + data separately (rather than a typed
    /// <c>D2Result&lt;T&gt;</c>) so both sources' differing data nullability annotations
    /// converge on one code path — <c>D2Result&lt;T&gt;</c> is invariant in <c>T</c>.
    /// </summary>
    /// <param name="envelope">The fetch result envelope (success/failure + status/code).</param>
    /// <param name="data">The leaf keyring DTO, if the fetch produced one.</param>
    /// <returns>A keyring result.</returns>
    internal static D2Result<PayloadCryptoKeyring> ToKeyringResult(
        D2Result envelope, GetKeyringOutput? data)
    {
        if (envelope.Failed)
            return D2Result<PayloadCryptoKeyring>.BubbleFail(envelope);

        if (data is null)
            return D2Result<PayloadCryptoKeyring>.ServiceUnavailable();

        return data.ToPayloadCryptoKeyring();
    }
}
