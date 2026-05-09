// -----------------------------------------------------------------------
// <copyright file="SampleRotationEvent.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Messaging;

using D2.Shared.Messaging;

/// <summary>Test fixture: a message type carrying [MqPub] referencing the
/// real <c>AuthKeyRotated</c> spec entry (which is plaintext per the spec).
/// Used by tests that exercise the plaintext code path through the
/// composer / dispatcher.</summary>
[MqPub(MqMessages.AuthKeyRotated)]
public sealed partial class SampleRotationEvent;
