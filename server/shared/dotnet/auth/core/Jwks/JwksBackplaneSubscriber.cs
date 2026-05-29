// -----------------------------------------------------------------------
// <copyright file="JwksBackplaneSubscriber.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Jwks;

using D2.Shared.Auth.Abstractions.Jwks;
using D2.Shared.Auth.Telemetry;
using D2.Shared.Caching;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Subscribes to <see cref="ICacheInvalidationBackplane"/> for cluster-wide
/// JWKS rotation events. On match, calls
/// <see cref="IJwksProvider.RefreshAsync(CancellationToken)"/> — which goes
/// through <see cref="HttpJwksProvider"/>'s singleflight + cooldown so
/// concurrent events don't storm Edge.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Backplane is OPTIONAL</strong> — when
/// <see cref="ICacheInvalidationBackplane"/> is not registered in DI, the
/// subscriber logs a startup warning and no-ops. Single-instance deployments
/// don't need cluster invalidation; multi-instance deployments without the
/// backplane fall back to ConfigurationManager's
/// <c>AutomaticRefreshInterval</c> (default 24h) for catching key rotations
/// — fine but slow.
/// </para>
/// <para>
/// <strong>Idempotent handler</strong> — multiple deliveries of the same
/// invalidation key produce the same outcome (cooldown gate keeps storms
/// bounded; refresh result is the same).
/// </para>
/// <para>
/// <strong>Reconnect contract</strong> — the subscription is established once
/// at <see cref="StartAsync"/> via <see cref="ICacheInvalidationBackplane.Subscribe"/>.
/// The contract delegates connection / channel resilience to the backplane
/// implementation: a conformant impl (e.g. <c>RedisCacheInvalidationBackplane</c>
/// in <c>D2.Shared.Caching.Distributed.Redis</c>) auto-reconnects internally
/// and resumes delivering events to the same handler lambda without
/// requiring re-subscription. If a future backplane impl breaks this
/// contract, this subscriber will silently miss events after the first
/// disconnect — track via the absence of <c>trigger=backplane_rotation</c>
/// counter increments.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
internal sealed class JwksBackplaneSubscriber : IHostedService, IAsyncDisposable
{
    private readonly IJwksProvider r_jwksProvider;
    private readonly ICacheInvalidationBackplane? r_backplane;
    private readonly AuthOptions r_options;
    private readonly ILogger<JwksBackplaneSubscriber> r_logger;
    private IAsyncDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwksBackplaneSubscriber"/> class.
    /// </summary>
    /// <param name="jwksProvider">The JWKS provider whose RefreshAsync to call on event.</param>
    /// <param name="options">The auth options snapshot.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="backplane">
    /// The optional backplane. When not registered (single-instance deploys
    /// or test fixtures), the subscriber logs a startup warning and no-ops.
    /// </param>
    [MustDisposeResource(false)]
    public JwksBackplaneSubscriber(
        IJwksProvider jwksProvider,
        IOptions<AuthOptions> options,
        ILogger<JwksBackplaneSubscriber> logger,
        ICacheInvalidationBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(jwksProvider);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_jwksProvider = jwksProvider;
        r_options = options.Value;
        r_logger = logger;
        r_backplane = backplane;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_backplane is null)
        {
            r_logger.JwksBackplaneAbsent();
            return Task.CompletedTask;
        }

        var expectedKey = r_options.Jwks.BackplaneChannelKey;
        _subscription = r_backplane.Subscribe(async (key, ct) =>
        {
            // Match exact key; ignore unrelated invalidations (session-revoked,
            // other domain key-rotated, etc.).
            if (!string.Equals(key, expectedKey, StringComparison.Ordinal))
                return;

            // Refresh — singleflight + cooldown inside HttpJwksProvider make
            // this safe to call repeatedly. Outcome captured by the provider's
            // own telemetry; we don't double-count.
            await r_jwksProvider.RefreshAsync(ct).ConfigureAwait(false);
            AuthTelemetry.SR_JwksFetches.Add(
                1,
                new KeyValuePair<string, object?>(
                    AuthTelemetryTags.JwksFetches.TAG_TRIGGER,
                    AuthTelemetryTags.JwksFetches.Trigger.BACKPLANE_ROTATION),
                new KeyValuePair<string, object?>(
                    AuthTelemetryTags.JwksFetches.TAG_OUTCOME,
                    AuthTelemetryTags.JwksFetches.Outcome.RECEIVED));
        });

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
            _subscription = null;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_subscription is not null)
        {
            await _subscription.DisposeAsync().ConfigureAwait(false);
            _subscription = null;
        }
    }
}
