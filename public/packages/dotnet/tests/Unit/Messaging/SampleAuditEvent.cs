// -----------------------------------------------------------------------
// <copyright file="SampleAuditEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Messaging;

/// <summary>Test fixture: a message type carrying [MqPub] referencing the
/// real <c>AuthKeyRotated</c> spec entry. Used by composer / dispatcher /
/// resolver tests that need a "happy-path" type with a working descriptor.
/// The CLR FQN deliberately differs from the spec entry's
/// <c>messageType</c> so the resolver throws on FQN mismatch — the actual
/// FQN-matching happy path is exercised in integration tests via real
/// production message types when those land. For composer-only tests, we
/// pass a hand-built <see cref="MqMessageDescriptor"/> directly and skip
/// the resolver.</summary>
[MqPub(MqMessages.AuthKeyRotated)]
public sealed partial class SampleAuditEvent;
