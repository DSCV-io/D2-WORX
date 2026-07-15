// -----------------------------------------------------------------------
// <copyright file="SampleRotationEvent.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Messaging;

using DcsvIo.D2.Messaging;

/// <summary>Test fixture: a message type carrying [MqPub] referencing the
/// real <c>AuthKeyRotated</c> spec entry (which is plaintext per the spec).
/// Used by tests that exercise the plaintext code path through the
/// composer / dispatcher.</summary>
[MqPub(MqMessages.AuthKeyRotated)]
public sealed partial class SampleRotationEvent;
