// -----------------------------------------------------------------------
// <copyright file="IntegrationPlaintextEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Messaging;

/// <summary>Integration message fixture exercising the plaintext code path.
/// The resolver descriptor is pre-seeded by
/// <see cref="IntegrationMessageFixtures"/>, NOT by an <c>[MqPub]</c>
/// attribute — see <see cref="IntegrationAuditEvent"/> for the rationale.</summary>
public sealed class IntegrationPlaintextEvent
{
    /// <summary>Gets or sets the test correlation marker.</summary>
    public string? Marker { get; set; }
}
