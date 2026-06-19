// -----------------------------------------------------------------------
// <copyright file="IIssueWorkloadCertificateHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Issues a short-lived workload leaf certificate signed by the active issuing
/// intermediate certificate authority, and writes a leaf-issuance audit entry.
/// </summary>
public interface IIssueWorkloadCertificateHandler
    : IHandler<IssueWorkloadCertificateInput, IssueWorkloadCertificateOutput>;
