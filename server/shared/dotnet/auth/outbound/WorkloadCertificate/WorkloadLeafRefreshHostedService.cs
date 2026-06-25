// -----------------------------------------------------------------------
// <copyright file="WorkloadLeafRefreshHostedService.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Outbound.WorkloadCertificate;

using D2.Shared.Auth.Outbound.Telemetry;
using JetBrains.Annotations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Background service that proactively reissues the cached workload leaf
/// certificate before it expires. Aims to keep <see cref="WorkloadLeafCache"/>
/// always populated so on-demand callers never trigger a synchronous reissue on the
/// hot path. On the issuer being unreachable, the warning logs but the existing
/// still-valid leaf continues to be presented until it actually expires.
/// </summary>
[MustDisposeResource(false)]
internal sealed class WorkloadLeafRefreshHostedService : BackgroundService
{
    /// <summary>
    /// Polling cadence for the reissue-check loop. Independent of the configured
    /// <see cref="AuthOutboundOptions.WorkloadLeafRefreshLeadTime"/>; the lead-time
    /// defines "how early before expiry to reissue" while this constant defines "how
    /// often to wake up and check whether reissue is due." 30 s is fine — leaf TTLs
    /// are hours, so 30 s of jitter on reissue timing is irrelevant.
    /// </summary>
    private static readonly TimeSpan sr_pollInterval = TimeSpan.FromSeconds(30);

    private readonly WorkloadLeafClient r_client;
    private readonly WorkloadLeafCache r_cache;
    private readonly AuthOutboundOptions r_options;
    private readonly ILogger<WorkloadLeafRefreshHostedService> r_logger;
    private readonly TimeProvider r_clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkloadLeafRefreshHostedService"/> class.
    /// </summary>
    /// <param name="client">The leaf client used to reissue fresh leaves.</param>
    /// <param name="cache">The shared per-process live-leaf cache.</param>
    /// <param name="options">Outbound auth options.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="clock">The time provider (overridable for tests).</param>
    [MustDisposeResource(false)]
    public WorkloadLeafRefreshHostedService(
        WorkloadLeafClient client,
        WorkloadLeafCache cache,
        IOptions<AuthOutboundOptions> options,
        ILogger<WorkloadLeafRefreshHostedService> logger,
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
        // Acquire a leaf immediately at startup so the first outbound mTLS handshake
        // hits a populated cache.
        var startupResult = await r_client.ForceReissueAsync(stoppingToken);

        if (startupResult.Success)
        {
            var acquired = r_cache.PeekRaw();

            if (acquired is not null)
                r_logger.WorkloadLeafStartupAcquireSucceeded(acquired.NotAfter.ToDateTimeOffset());
        }
        else
        {
            r_logger.WorkloadLeafStartupAcquireFailed(sr_pollInterval);
        }

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
        var now = Instant.FromDateTimeOffset(r_clock.GetUtcNow());
        var leadTime = r_options.WorkloadLeafRefreshLeadTime;

        var refreshDue =
            snapshot is null
            || (snapshot.NotAfter - now).ToTimeSpan() <= leadTime;

        if (!refreshDue)
            return;

        var result = await r_client.ForceReissueAsync(ct);

        // Don't let the loop die on a single failed reissue — log + retry on the
        // next tick. The still-valid cached leaf (if any) keeps being presented
        // until it actually expires.
        if (!result.Success)
            r_logger.WorkloadLeafRefreshTickFailed();
    }
}
