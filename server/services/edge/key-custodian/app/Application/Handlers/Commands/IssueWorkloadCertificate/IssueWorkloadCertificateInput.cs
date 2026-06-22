// -----------------------------------------------------------------------
// <copyright file="IssueWorkloadCertificateInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

/// <summary>
/// Input to <c>IssueWorkloadCertificate</c>: which workload to issue a leaf
/// certificate for.
/// </summary>
/// <param name="WorkloadServiceId">
/// The raw workload service identifier (validated in-handler via
/// <c>WorkloadIdentity.Create</c>).
/// </param>
public sealed record IssueWorkloadCertificateInput(string? WorkloadServiceId);
