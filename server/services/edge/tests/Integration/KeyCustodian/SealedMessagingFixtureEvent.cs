// -----------------------------------------------------------------------
// <copyright file="SealedMessagingFixtureEvent.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Integration.KeyCustodian;

/// <summary>
/// Sealed-messaging integration fixture event. Its resolver descriptor (exchange +
/// the REAL sealed <c>audit</c> encryption domain) is pre-seeded through the
/// test-only <c>MessageWireResolver.RegisterForTesting</c> seam — never a
/// production message; the production registry carries only real spec messages.
/// </summary>
public sealed class SealedMessagingFixtureEvent
{
    /// <summary>Gets or sets the fixture payload content.</summary>
    public string? Content { get; set; }
}
