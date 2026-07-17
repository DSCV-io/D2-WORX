// -----------------------------------------------------------------------
// <copyright file="HandlerDispatcherFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Subscribing;

using System.Collections.Concurrent;

/// <summary>
/// Pre-builds typed <see cref="IHandlerDispatcher"/> instances per
/// <see cref="ISubscriberRegistration"/>. Reflection runs once per
/// registration at startup; per-message dispatch is then a dictionary
/// lookup + virtual call.
/// </summary>
internal sealed class HandlerDispatcherFactory
{
    private readonly ConcurrentDictionary<string, IHandlerDispatcher> r_byQueue =
        new(StringComparer.Ordinal);

    private readonly SubscriberRegistry r_registry;

    /// <summary>Initializes the factory.</summary>
    /// <param name="registry">Source of subscriber registrations.</param>
    public HandlerDispatcherFactory(SubscriberRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        r_registry = registry;
    }

    /// <summary>Returns a dispatcher for the named queue (built on demand,
    /// cached).</summary>
    /// <param name="queueName">Queue name (matches a registered subscriber).</param>
    /// <exception cref="InvalidOperationException">No subscriber registered
    /// for that queue, or the registration's types are incompatible with
    /// <c>BaseHandler&lt;TSelf, TIn, Unit&gt;</c>.</exception>
    public IHandlerDispatcher GetForQueue(string queueName)
    {
        return r_byQueue.GetOrAdd(queueName, BuildDispatcher);
    }

    private IHandlerDispatcher BuildDispatcher(string queueName)
    {
        var reg = r_registry.All.FirstOrDefault(
            r => string.Equals(r.ResolvedQueueName, queueName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"No subscriber registered for queue '{queueName}'.");

        // Compose the closed generic. The handler must derive from
        // BaseHandler<TSelf, TMessage, Unit> so the dispatcher's where-clauses
        // are satisfied. Activator.CreateInstance throws a clear exception
        // otherwise.
        var openGeneric = typeof(TypedHandlerDispatcher<,>);
        var closedGeneric = openGeneric.MakeGenericType(reg.HandlerType, reg.MessageType);
        var instance = Activator.CreateInstance(closedGeneric)
            ?? throw new InvalidOperationException(
                $"Failed to construct dispatcher for queue '{queueName}' "
                + $"(handler={reg.HandlerType.FullName}, "
                + $"message={reg.MessageType.FullName}). Verify the handler "
                + $"derives from BaseHandler<{reg.HandlerType.Name}, "
                + $"{reg.MessageType.Name}, Unit>.");
        return (IHandlerDispatcher)instance;
    }
}
