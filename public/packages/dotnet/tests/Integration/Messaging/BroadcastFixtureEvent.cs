// -----------------------------------------------------------------------
// <copyright file="BroadcastFixtureEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

/// <summary>Integration message fixture exercising the plaintext FANOUT
/// broadcast code path — the redaction-broadcast shape (one publish, every
/// bound queue receives). The resolver descriptor (a <c>fanout</c> exchange,
/// plaintext domain) is pre-seeded by
/// <see cref="IntegrationMessageFixtures"/>, NOT by an <c>[MqPub]</c>
/// attribute — see <see cref="IntegrationAuditEvent"/> for the rationale.</summary>
public sealed class BroadcastFixtureEvent
{
    /// <summary>Gets or sets the test correlation marker.</summary>
    public string? Marker { get; set; }
}
