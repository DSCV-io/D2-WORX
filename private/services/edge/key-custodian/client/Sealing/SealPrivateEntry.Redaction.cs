// -----------------------------------------------------------------------
// <copyright file="SealPrivateEntry.Redaction.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace -- a hand-authored partial for the
// protobuf-generated SealPrivateEntry MUST declare the proto's own namespace
// (D2.Services.Protos.KeyCustodian.V2Alpha) to merge with it; see the summary.
namespace D2.Services.Protos.KeyCustodian.V2Alpha;

/// <summary>
/// Secret-redaction declaration for the generated seal-private-keyring wire proto. The
/// protobuf compiler cannot attach <see cref="RedactDataAttribute"/> to its output, so
/// this hand-authored partial declaration carries the type-level attribute instead — the
/// Serilog destructuring policy then masks the ENTIRE entry (kid + raw PKCS#8 private key
/// bytes) as <c>[REDACTED: SecretInformation]</c> wherever a destructured log capture
/// would otherwise render it, including recursively through
/// <c>GetOrLazyProvisionOwnSealPrivateKeyOutput.Entries</c> and its full gRPC reply body.
/// Mirrors the <c>[RedactData]</c> the emitted leaf DTO
/// (<c>D2.Edge.KeyCustodian.Client.Sealing.SealPrivateEntry.PrivatePkcs8</c>) already
/// carries, and the sibling <c>KeyringEntry.Redaction.cs</c> partial. Defense-in-depth:
/// no code path logs this wire proto today; the boundary mapper immediately supersedes it
/// with the redacted leaf DTO.
/// </summary>
[RedactData(Reason = RedactReason.SecretInformation)]
public sealed partial class SealPrivateEntry;
