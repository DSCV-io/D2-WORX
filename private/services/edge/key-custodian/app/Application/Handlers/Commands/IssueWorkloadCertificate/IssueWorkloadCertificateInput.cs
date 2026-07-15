// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Input to <c>IssueWorkloadCertificate</c>: the workload's PKCS#10
/// certificate-signing request.
/// </summary>
/// <remarks>
/// There is deliberately NO subject field — the leaf's subject-alternative-name is
/// always derived from the authenticated mTLS peer identity on the established
/// request context, never from the input (structural self-issue). A CSR is PUBLIC
/// material by construction (public key + request metadata + a self-signature) —
/// it never carries the private key, so nothing here is redacted.
/// </remarks>
/// <param name="CsrDer">
/// The DER-encoded PKCS#10 certificate-signing request (validated in-handler via
/// the CSR verification rule: size cap, parse, proof-of-possession, P-256 curve).
/// </param>
public sealed record IssueWorkloadCertificateInput(byte[]? CsrDer);
