// -----------------------------------------------------------------------
// <copyright file="SessionRevokedBackplaneSubscriber.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Sessions;

using D2.Shared.Auth.Telemetry;
using D2.Shared.Caching;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Telemetry-only observer for cluster-wide session-revoke events.
/// Subscribes to <see cref="ICacheInvalidationBackplane"/> and increments
/// <see cref="AuthTelemetry.SessionLivenessChecks"/> with
/// <c>outcome=backplane_revoked</c> for every key matching the configured
/// session-cache prefix.
/// </summary>
/// <remarks>
/// <para>
/// <strong>NOT required for correctness</strong> —
/// <see cref="D2.Shared.Caching.Tiered.DefaultTieredCache"/> auto-subscribes
/// to the same backplane in its own constructor and drops
/// matching L1 entries cluster-wide. This subscriber sits alongside,
/// observing the same events purely for the ops dashboards' "revocations
/// per minute" metric — otherwise only visible as downstream
/// <c>IsAliveAsync = false</c> results which are noisier signal.
/// </para>
/// <para>
/// <strong>Backplane is OPTIONAL</strong> — when not registered (single-instance
/// deploys, test fixtures), the subscriber logs a one-line warning and
/// no-ops at runtime.
/// </para>
/// </remarks>
[MustDisposeResource(false)]
internal sealed class SessionRevokedBackplaneSubscriber : IHostedService, IAsyncDisposable
{
    private readonly ICacheInvalidationBackplane? r_backplane;
    private readonly AuthOptions r_options;
    private readonly ILogger<SessionRevokedBackplaneSubscriber> r_logger;
    private IAsyncDisposable? _subscription;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="SessionRevokedBackplaneSubscriber"/> class.
    /// </summary>
    /// <param name="options">The auth options snapshot.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="backplane">
    /// The optional backplane. When not registered, the subscriber no-ops.
    /// </param>
    [MustDisposeResource(false)]
    public SessionRevokedBackplaneSubscriber(
        IOptions<AuthOptions> options,
        ILogger<SessionRevokedBackplaneSubscriber> logger,
        ICacheInvalidationBackplane? backplane = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        r_options = options.Value;
        r_logger = logger;
        r_backplane = backplane;
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (r_backplane is null)
        {
            r_logger.SessionRevokedBackplaneAbsent();
            return Task.CompletedTask;
        }

        var prefix = r_options.Sessions.CacheKeyPrefix;
        _subscription = r_backplane.Subscribe((key, _) =>
        {
            if (!key.StartsWith(prefix, StringComparison.Ordinal))
                return ValueTask.CompletedTask;

            AuthTelemetry.SessionLivenessChecks.Add(
                1, new KeyValuePair<string, object?>("outcome", "backplane_revoked"));
            return ValueTask.CompletedTask;
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
