// -----------------------------------------------------------------------
// <copyright file="EdgeHostIdentity.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Composition;

/// <summary>
/// Edge host workload identity constants (ServiceId for establishment + self-issue SAN).
/// </summary>
public static class EdgeHostIdentity
{
    /// <summary>
    /// Edge workload ServiceId bound on establishment options and CSR self-issue.
    /// </summary>
    public const string SERVICE_ID = "edge";
}
