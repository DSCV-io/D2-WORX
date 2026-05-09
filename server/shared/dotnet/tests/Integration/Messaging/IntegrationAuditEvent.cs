// -----------------------------------------------------------------------
// <copyright file="IntegrationAuditEvent.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Integration.Messaging;

/// <summary>Integration message fixture exercising the encrypted code path.
/// The resolver descriptor (exchange, encryption domain) is pre-seeded by
/// <see cref="IntegrationMessageFixtures"/>, NOT by an <c>[MqPub]</c>
/// attribute — the production registry only covers real domain messages,
/// and integration fixtures use the test-only registration seam.</summary>
public sealed class IntegrationAuditEvent
{
    /// <summary>Gets or sets the test correlation marker.</summary>
    public string? Marker { get; set; }
}
