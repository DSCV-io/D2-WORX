// -----------------------------------------------------------------------
// <copyright file="IIssueWorkloadCertificateHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Issues a short-lived workload leaf certificate from a PKCS#10
/// certificate-signing request — the leaf's subject-alternative-name is always the
/// authenticated mTLS peer identity, never a caller-supplied subject — signed by
/// the active issuing intermediate via the isolated leaf-signing capability, and
/// writes a leaf-issuance audit entry. The single chokepoint BOTH planes
/// (in-process façade + gRPC) flow through: authority gate, scope check, CSR
/// verification, audit, and deny telemetry all live here.
/// </summary>
public interface IIssueWorkloadCertificateHandler
    : IHandler<IssueWorkloadCertificateInput, IssueWorkloadCertificateOutput>;
