// -----------------------------------------------------------------------
// <copyright file="SubscriberRegistration.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging;

/// <summary>
/// Default <see cref="ISubscriberRegistration"/> implementation. Internal —
/// the assembly-scan helper + the explicit programmatic helper produce these.
/// </summary>
internal sealed record SubscriberRegistration(
    Type HandlerType,
    Type MessageType,
    MqSubscriptionDescriptor Descriptor,
    string ResolvedQueueName) : ISubscriberRegistration;
