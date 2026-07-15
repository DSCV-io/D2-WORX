// -----------------------------------------------------------------------
// <copyright file="SubscriberRegistration.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// Default <see cref="ISubscriberRegistration"/> implementation. Internal —
/// the assembly-scan helper + the explicit programmatic helper produce these.
/// </summary>
internal sealed record SubscriberRegistration(
    Type HandlerType,
    Type MessageType,
    MqSubscriptionDescriptor Descriptor,
    string ResolvedQueueName) : ISubscriberRegistration;
