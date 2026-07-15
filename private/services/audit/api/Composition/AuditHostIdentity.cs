// -----------------------------------------------------------------------
// <copyright file="AuditHostIdentity.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Audit.Api.Composition;

/// <summary>
/// Audit host identity constants — ServiceId for establishment + SPIFFE.
/// </summary>
public static class AuditHostIdentity
{
    /// <summary>
    /// Workload ServiceId for the Audit process
    /// (<c>spiffe://d2.internal/workload/audit</c>).
    /// </summary>
    public const string SERVICE_ID = "audit";
}
