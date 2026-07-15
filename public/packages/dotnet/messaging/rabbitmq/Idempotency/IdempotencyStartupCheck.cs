// -----------------------------------------------------------------------
// <copyright file="IdempotencyStartupCheck.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Idempotency;

using DcsvIo.D2.Caching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Fails host startup when one or more subscribers opt into idempotency
/// (the <c>idempotency</c> field on the spec entry → carried as
/// <see cref="MqSubscriptionDescriptor.Idempotency"/>) but no functional
/// <see cref="IDistributedCache"/> AND no operator-provided
/// <see cref="IMessageIdempotencyStore"/> are registered. Without this
/// guard the consumer silently no-ops the idempotency check on every
/// delivery — the safety feature appears configured but does nothing.
/// </summary>
internal sealed class IdempotencyStartupCheck : IHostedService
{
    private readonly SubscriberRegistry r_registry;
    private readonly IServiceProvider r_serviceProvider;

    /// <summary>Initializes the check.</summary>
    /// <param name="registry">Subscriber registry.</param>
    /// <param name="serviceProvider">Used to probe for an
    /// <see cref="IDistributedCache"/> registration.</param>
    public IdempotencyStartupCheck(
        SubscriberRegistry registry, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        r_registry = registry;
        r_serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var idempotentSubs = r_registry.All
            .Where(r => r.Descriptor.Idempotency)
            .ToList();
        if (idempotentSubs.Count == 0) return Task.CompletedTask;

        // CacheIdempotencyStore needs IDistributedCache. If the cache is
        // present, the default store works — early return. If absent, the
        // operator may still have plugged in their own store (e.g. tests
        // with an in-memory fake) — try resolving the actual instance and
        // accept anything that's not the default cache-backed type.
        var hasCache = r_serviceProvider.GetService<IDistributedCache>() is not null;
        if (hasCache) return Task.CompletedTask;

        try
        {
            var resolved = r_serviceProvider.GetService<IMessageIdempotencyStore>();
            if (resolved is not null and not CacheIdempotencyStore)
                return Task.CompletedTask;
        }
        catch
        {
            // Resolution threw — most likely the default CacheIdempotencyStore
            // failed to construct because IDistributedCache is missing, which
            // is exactly the misconfiguration this check exists to surface.
            // Fall through to the throw below.
        }

        var subList = string.Join(
            ", ",
            idempotentSubs.Select(r =>
                $"{r.HandlerType.Name} (queue={r.ResolvedQueueName})"));
        throw new InvalidOperationException(
            $"One or more subscriptions declare idempotency=true in "
            + $"mq-subscriptions.spec.json but no IDistributedCache is "
            + $"registered to back the default IMessageIdempotencyStore. "
            + $"Either register IDistributedCache (e.g. "
            + $"AddD2DistributedCacheRedis) or set idempotency=false on "
            + $"the subscription spec entry. Affected: {subList}.");
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
