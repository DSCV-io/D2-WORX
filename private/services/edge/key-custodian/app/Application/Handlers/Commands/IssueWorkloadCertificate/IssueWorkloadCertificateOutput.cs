// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Result of <c>IssueWorkloadCertificate</c>: the freshly-issued leaf certificate,
/// the issuing-intermediate certificate, and the validity window.
/// </summary>
/// <remarks>
/// Unlike the managed-key lifecycle commands (which return the non-sensitive
/// <c>KeySummary</c>), issuance returns the actual certificate material to the
/// caller — that is the whole point of the operation. ALL of it is public: the
/// workload generated its own keypair and submitted a CSR, so the carried
/// <see cref="IssuedWorkloadCertificate"/> holds only certificates + validity —
/// no private key exists anywhere on this path, nothing needs redaction, and
/// there is nothing to zero.
/// </remarks>
/// <param name="Certificate">The issued leaf + issuing-intermediate certificates and validity.</param>
public sealed record IssueWorkloadCertificateOutput(IssuedWorkloadCertificate Certificate);
