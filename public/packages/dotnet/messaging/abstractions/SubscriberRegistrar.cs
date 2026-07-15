// -----------------------------------------------------------------------
// <copyright file="SubscriberRegistrar.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Shared logic between the assembly scanner and the explicit programmatic
/// helper. Resolves a subscription descriptor by constant, validates the
/// handler type, and registers it with DI + the
/// <see cref="SubscriberRegistry"/>.
/// </summary>
internal static class SubscriberRegistrar
{
    /// <summary>Constant used as the per-process suffix delimiter on
    /// auto-suffixed fanout-exclusive queue names. Choosing a delimiter
    /// the broker permits in queue names AND that ops tooling can spot
    /// at a glance.</summary>
    private const string _SUFFIX_DELIMITER = ".";

    /// <summary>Per-process suffix appended to fanout-exclusive queue names.
    /// Generated once per process — every <c>FanoutExclusiveAutoDelete</c>
    /// subscription in the same process shares the same suffix, which is
    /// fine because each subscription has a different prefix
    /// (<see cref="MqSubscriptionDescriptor.QueueName"/>).</summary>
    private static readonly string sr_processSuffix =
        Guid.CreateVersion7().ToString("N")[..8];

    /// <summary>Validates + registers a handler type discovered via
    /// <see cref="MqSubAttribute"/>. Throws on misconfiguration so failure
    /// surfaces at composition time rather than at first delivery.</summary>
    /// <param name="services">The DI container.</param>
    /// <param name="handlerType">Concrete handler class.</param>
    /// <param name="constant">Subscription constant from the
    /// <c>[MqSub]</c> attribute.</param>
    public static void Register(
        IServiceCollection services, Type handlerType, string constant)
    {
        if (!MqSubscriptionsRegistry.ByConstant.TryGetValue(constant, out var descriptor))
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType.FullName}' carries [MqSub(\"{constant}\")] but "
                + $"'{constant}' is not in MqSubscriptionsRegistry. Likely a stale "
                + $"build after a spec edit, or the constant was renamed without "
                + $"updating the attribute. Rebuild; if it persists, verify the "
                + $"name in contracts/mq-subscriptions/mq-subscriptions.spec.json.");
        }

        var messageType = ResolveHandlerMessageType(handlerType);

        if (!string.Equals(
            descriptor.MessageTypeName,
            messageType.FullName,
            StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Handler '{handlerType.FullName}' carries [MqSub(\"{constant}\")] "
                + $"but its message type generic parameter is '{messageType.FullName}', "
                + $"while the spec entry's messageType is '{descriptor.MessageTypeName}'. "
                + $"They must match — either change the handler's BaseHandler<...,X,Unit> "
                + $"generic argument or update the spec.");
        }

        services.AddTransient(handlerType);
        var resolvedQueueName = ResolveQueueName(descriptor);
        services.AddSingleton<ISubscriberRegistration>(_ => new SubscriberRegistration(
            HandlerType: handlerType,
            MessageType: messageType,
            Descriptor: descriptor,
            ResolvedQueueName: resolvedQueueName));
    }

    /// <summary>For <see cref="QueuePattern.FanoutExclusiveAutoDelete"/>,
    /// auto-appends a per-process token to the spec's queue name so
    /// multi-replica services don't collide on the broker's exclusive-queue
    /// lock. Other patterns return the spec name verbatim.</summary>
    /// <param name="descriptor">The subscription descriptor.</param>
    public static string ResolveQueueName(MqSubscriptionDescriptor descriptor)
    {
        if (descriptor.Pattern == QueuePattern.FanoutExclusiveAutoDelete)
        {
            return descriptor.QueueName + _SUFFIX_DELIMITER + sr_processSuffix;
        }

        return descriptor.QueueName;
    }

    private static Type ResolveHandlerMessageType(Type handlerType)
    {
        // Walk up the inheritance chain looking for BaseHandler<TSelf, TIn, TOut>.
        var t = handlerType.BaseType;
        while (t is not null)
        {
            if (t.IsGenericType
                && t.GetGenericTypeDefinition().FullName
                    == "DcsvIo.D2.Handler.BaseHandler`3")
            {
                var args = t.GetGenericArguments();

                // BaseHandler<TSelf, TInput, TOutput> — TInput is index 1.
                return args[1];
            }

            t = t.BaseType;
        }

        throw new InvalidOperationException(
            $"Handler '{handlerType.FullName}' carries [MqSub] but does not derive "
            + $"from BaseHandler<TSelf, TMessage, Unit>. The MqSub assembly "
            + $"scanner only registers BaseHandler subclasses.");
    }
}
