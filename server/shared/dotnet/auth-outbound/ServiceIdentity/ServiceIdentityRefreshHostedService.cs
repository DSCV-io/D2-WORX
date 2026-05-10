// -----------------------------------------------------------------------
// <copyright file="ServiceIdentityRefreshHostedService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.ServiceIdentity;

using D2.Shared.Auth.Outbound.Telemetry;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Background service that proactively refreshes the cached service-identity
/// token before it expires. Aims to keep <see cref="ServiceIdentityCache"/>
/// always populated so that on-demand callers never trigger a synchronous
/// refresh on the hot path. On Edge unreachable, the warning logs but the
/// existing still-valid token continues to be served until it actually expires.
/// </summary>
[MustDisposeResource(false)]
internal sealed class ServiceIdentityRefreshHostedService : BackgroundService
{
    /// <summary>
    /// Polling cadence for the refresh check loop. Independent of the
    /// configured <see cref="AuthOutboundOptions.ServiceIdentityRefreshLeadTime"/>;
    /// the lead-time defines "how early before expiry to refresh" while this
    /// constant defines "how often to wake up and check whether refresh is
    /// due." 5 s is fine — token TTLs are minutes, so 5 s of jitter on
    /// refresh timing is irrelevant.
    /// </summary>
    private static readonly TimeSpan sr_pollInterval = TimeSpan.FromSeconds(5);

    private readonly HttpServiceIdentityClient r_client;
    private readonly ServiceIdentityCache r_cache;
    private readonly AuthOutboundOptions r_options;
    private readonly ILogger<ServiceIdentityRefreshHostedService> r_logger;
    private readonly TimeProvider r_clock;

    /// <summary>Initializes a new instance of the <see cref="ServiceIdentityRefreshHostedService"/> class.</summary>
    /// <param name="client">The service-identity client used to fetch fresh tokens.</param>
    /// <param name="cache">The shared per-process token cache.</param>
    /// <param name="options">Outbound auth options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    [MustDisposeResource(false)]
    public ServiceIdentityRefreshHostedService(
        HttpServiceIdentityClient client,
        ServiceIdentityCache cache,
        IOptions<AuthOutboundOptions> options,
        ILogger<ServiceIdentityRefreshHostedService> logger,
        TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(clock);

        r_client = client;
        r_cache = cache;
        r_options = options.Value;
        r_logger = logger;
        r_clock = clock;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Acquire a token immediately at startup so the first on-demand caller
        // hits a populated cache. Bootstrap-order dependency: KeyringClient +
        // JwksProvider need this token to authenticate their own gRPC / HTTP
        // calls to Edge.
        var startupResult = await r_client.ForceRefreshAsync(stoppingToken);
        if (!startupResult.Success)
            r_logger.ServiceIdentityStartupAcquireFailed(sr_pollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(sr_pollInterval, r_clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var snapshot = r_cache.PeekRaw();
        var now = r_clock.GetUtcNow();
        var leadTime = r_options.ServiceIdentityRefreshLeadTime;

        var refreshDue =
            snapshot is null
            || snapshot.ExpiresAt - now <= leadTime;

        if (!refreshDue)
            return;

        var result = await r_client.ForceRefreshAsync(ct);

        // Don't let the loop die on a single failed refresh — log + retry
        // on the next tick. The still-valid cached token (if any) keeps
        // serving until it actually expires.
        if (!result.Success)
            r_logger.ServiceIdentityRefreshTickFailed();
    }
}
