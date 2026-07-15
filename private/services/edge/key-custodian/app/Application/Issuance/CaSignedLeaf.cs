// -----------------------------------------------------------------------
// <copyright file="CaSignedLeaf.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Issuance;

/// <summary>
/// The result of one issuance leaf-signing: the issued certificate material plus
/// the kid of the issuing intermediate that signed it (the audit row + the
/// issuance forensic log both name the signer).
/// </summary>
/// <remarks>
/// All-public material — the carried <see cref="IssuedWorkloadCertificate"/> holds
/// only certificates and the validity window (the leaf private key never enters
/// KeyCustodian), and a kid is a non-PII opaque label.
/// </remarks>
/// <param name="Certificate">The issued leaf + issuing-intermediate certificate material.</param>
/// <param name="IssuerKid">The kid of the active issuing intermediate that signed the leaf.</param>
public sealed record CaSignedLeaf(IssuedWorkloadCertificate Certificate, Kid IssuerKid);
