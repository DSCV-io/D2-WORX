// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Result of <c>IssueWorkloadCertificate</c>: the freshly-issued leaf certificate,
/// its private key, the issuing-chain certificate, and the validity window.
/// </summary>
/// <remarks>
/// Unlike the managed-key lifecycle commands (which return the non-sensitive
/// <c>KeySummary</c>), issuance MUST return the actual leaf material to the caller
/// — that is the whole point of the operation. The carried
/// <see cref="IssuedWorkloadCertificate"/> holds a SECRET private key; the
/// zeroize responsibility transfers to the caller, which calls
/// <see cref="IssuedWorkloadCertificate.Zero"/> once it has installed the leaf.
/// KeyCustodian never persists the leaf private key.
/// </remarks>
/// <param name="Certificate">The issued leaf certificate, chain, private key, and validity.</param>
public sealed record IssueWorkloadCertificateOutput(IssuedWorkloadCertificate Certificate);
