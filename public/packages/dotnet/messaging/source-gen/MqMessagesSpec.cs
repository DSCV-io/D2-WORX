// -----------------------------------------------------------------------
// <copyright file="MqMessagesSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.SourceGen;

using System.Collections.Immutable;

/// <summary>The parsed root of <c>mq-messages.spec.json</c>.</summary>
/// <param name="Messages">All message entries in source order.</param>
internal sealed record MqMessagesSpec(ImmutableArray<MqMessageEntry> Messages);
