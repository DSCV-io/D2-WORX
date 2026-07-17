// -----------------------------------------------------------------------
// <copyright file="SubscriberRegistry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

/// <summary>
/// DI-singleton aggregating every <see cref="ISubscriberRegistration"/>
/// recorded via <c>AddD2Subscriber</c>. Read once at startup by the
/// transport's consumer host to declare topology and open consumer
/// channels.
/// </summary>
/// <remarks>
/// Validates queue-name uniqueness at construction time — DI resolution
/// fails fast on duplicate registrations rather than at consumer startup.
/// </remarks>
public sealed class SubscriberRegistry
{
    /// <summary>Constructed via DI from every
    /// <see cref="ISubscriberRegistration"/> registered as a singleton.</summary>
    /// <param name="registrations">All subscriber registrations the
    /// container resolved (in registration order).</param>
    /// <exception cref="InvalidOperationException">
    /// Two subscribers attempt to use the same queue name.
    /// </exception>
    public SubscriberRegistry(IEnumerable<ISubscriberRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        var list = registrations.ToArray();
        var byQueue = new Dictionary<string, ISubscriberRegistration>(StringComparer.Ordinal);
        foreach (var reg in list)
        {
            var queue = reg.ResolvedQueueName;
            if (byQueue.TryGetValue(queue, out var existing))
            {
                throw new InvalidOperationException(
                    $"Subscriber for queue '{queue}' registered twice "
                    + $"(handlers: {existing.HandlerType.FullName}, "
                    + $"{reg.HandlerType.FullName}). Each queue must have "
                    + "exactly one subscriber per process.");
            }

            byQueue[queue] = reg;
        }

        All = list;
    }

    /// <summary>Gets a snapshot of all registrations. Iteration order matches
    /// DI registration order.</summary>
    public IReadOnlyList<ISubscriberRegistration> All { get; }
}
