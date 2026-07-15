// -----------------------------------------------------------------------
// <copyright file="MqSubscriptionsSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System.Collections.Immutable;

/// <summary>The parsed root of <c>mq-subscriptions.spec.json</c>.</summary>
/// <param name="Subscriptions">All subscription entries in source order.</param>
internal sealed record MqSubscriptionsSpec(
    ImmutableArray<MqSubscriptionEntry> Subscriptions);
