// -----------------------------------------------------------------------
// <copyright file="KeyringEntry.Redaction.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace -- a hand-authored partial for the
// protobuf-generated KeyringEntry MUST declare the proto's own namespace
// (D2.Services.Protos.KeyCustodian.V2Alpha) to merge with it; see the summary.
namespace D2.Services.Protos.KeyCustodian.V2Alpha;

/// <summary>
/// Secret-redaction declaration for the generated keyring wire proto. The protobuf
/// compiler cannot attach <see cref="RedactDataAttribute"/> to its output, so this
/// hand-authored partial declaration carries the type-level attribute instead — the
/// Serilog destructuring policy then masks the ENTIRE entry (kid + raw AES key bytes)
/// as <c>[REDACTED: SecretInformation]</c> wherever a destructured log capture would
/// otherwise render it, including recursively through <c>GetKeyringOutput.Entries</c>
/// and a full <c>GetKeyringResponse</c>. Mirrors the <c>[RedactData]</c> the emitted
/// leaf DTO (<c>D2.Edge.KeyCustodian.Client.Keyring.KeyringEntry.KeyBytes</c>) already carries.
/// </summary>
[RedactData(Reason = RedactReason.SecretInformation)]
public sealed partial class KeyringEntry;
