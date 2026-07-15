// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadOpener.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Sealing;

using System.Collections.Concurrent;
using System.Linq;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Shared.Encryption;
using D2.Shared.Resilience.Retry;
using Microsoft.Extensions.Logging;

/// <summary>
/// A sealed, hot-swappable <see cref="IPayloadOpener"/> capability backed by this service's
/// KeyCustodian PRIVATE seal keyring — the sealed twin of <c>KeyringBackedPayloadCrypto</c>.
/// Holds its private key material in-process-memory-only, refreshes atomically on a rotation
/// event, and is the ONLY in-memory holder for its <c>seal:&lt;ownServiceId&gt;</c> keyring.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Least-privilege surface.</strong> The public members are <see cref="Open"/> /
/// <see cref="DisposeAsync"/> only — NO member ever returns the keyring or raw key bytes. Raw
/// material sits in a private volatile holder, zeroized on dispose. There is NO
/// <c>Seal</c> — the capability split is compile-time (opening is private-key-only).
/// </para>
/// <para>
/// <strong>Torn-read safety + displaced-keyring disposal.</strong> Identical to the symmetric
/// twin: the <c>(keyring, opener)</c> pair swaps as a single reference via
/// <see cref="Interlocked.Exchange{T}(ref T, T)"/>; a displaced private keyring is zeroized
/// after a ~30s grace scheduled off-thread; <see cref="DisposeAsync"/> drains and
/// force-zeroizes any holder still inside its grace at shutdown.
/// </para>
/// </remarks>
public sealed class KeyringBackedPayloadOpener : IPayloadOpener, IAsyncDisposable
{
    // Bounded, handler-side rotation-refresh retry (see the symmetric twin for the rationale).
    private const int _REFRESH_MAX_ATTEMPTS = 3;

    // ~30s bounds unzeroed private-key retention while making a use-after-dispose implausible.
    private static readonly TimeSpan sr_keyringDisposeGrace = TimeSpan.FromSeconds(30);

    // Base backoff between bounded rotation-refresh attempts (exponential, via RetryHelper).
    private static readonly TimeSpan sr_refreshBackoffBase = TimeSpan.FromSeconds(2);

    // A connected-but-unresponsive KeyCustodian must never hang host startup forever.
    private static readonly TimeSpan sr_startupFetchTimeout = TimeSpan.FromSeconds(30);

    private readonly string r_sealDomain;
    private readonly string r_ownServiceId;
    private readonly ISealingClient r_client;
    private readonly ILogger<KeyringBackedPayloadOpener> r_logger;
    private readonly IAsyncDisposable r_subscription;
    private readonly ConcurrentDictionary<PendingDispose, byte> r_pending = new();
    private readonly TimeSpan r_grace;
    private readonly RetryOptions<D2Result<RecipientPrivateKeyring>> r_refreshRetryOptions;

    private Holder _holder;
    private int _disposed;

    private KeyringBackedPayloadOpener(
        string ownServiceId,
        ISealingClient client,
        RecipientPrivateKeyring initialKeyring,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadOpener> logger,
        TimeSpan grace,
        RetryOptions<D2Result<RecipientPrivateKeyring>> refreshRetryOptions)
    {
        r_ownServiceId = ownServiceId;
        r_sealDomain = SealDomainName.For(ownServiceId);
        r_client = client;
        r_logger = logger;
        r_grace = grace;
        r_refreshRetryOptions = refreshRetryOptions;
        _holder = new Holder(initialKeyring, new PayloadOpener(initialKeyring));
        r_subscription = channel.Subscribe(r_sealDomain, OnRotationAsync);
    }

    /// <inheritdoc />
    public byte[] Open(ReadOnlySpan<byte> framed)
        => Volatile.Read(ref _holder).Opener.Open(framed);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Stop new rotations first, then drain + force-zeroize anything still in grace.
        await r_subscription.DisposeAsync().ConfigureAwait(false);

        var pending = r_pending.Keys.ToArray();

        foreach (var entry in pending)
            entry.Cancel();

        foreach (var entry in pending)
        {
            await entry.Task.ConfigureAwait(false);
            entry.Dispose();
        }

        // A rotation racing this dispose sets _disposed BEFORE swapping in a fresh holder,
        // so the post-swap re-check in SwapTo zeroizes any replacement this final dispose
        // does not observe. Reading the current holder here zeroizes the live one.
        Volatile.Read(ref _holder).Keyring.Dispose();
    }

    /// <summary>Returns the type name only — never any keyring contents.</summary>
    public override string ToString() => nameof(KeyringBackedPayloadOpener);

    /// <summary>
    /// Builds a <see cref="KeyringBackedPayloadOpener"/> for <paramref name="ownServiceId"/>,
    /// performing the initial private-keyring fetch synchronously (fail-loud) — a
    /// serving-before-ready window is worse than a deliberate one-time blocking boot fetch, and
    /// the generic host has no ambient <see cref="SynchronizationContext"/> to deadlock. Bounded
    /// by a startup timeout so an unresponsive KeyCustodian cannot hang host startup.
    /// </summary>
    /// <param name="ownServiceId">This service's own id (the sealed recipient identity).</param>
    /// <param name="client">The (internal) fetch seam.</param>
    /// <param name="channel">The rotation-notification channel to subscribe.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>A ready-to-serve opener capability.</returns>
    /// <exception cref="InvalidOperationException">The startup keyring fetch failed.</exception>
    internal static KeyringBackedPayloadOpener Create(
        string ownServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadOpener> logger)
        => Create(
            ownServiceId,
            client,
            channel,
            logger,
            sr_keyringDisposeGrace,
            _REFRESH_MAX_ATTEMPTS,
            sr_refreshBackoffBase);

    /// <summary>
    /// Test-only <c>Create</c> overload that injects the displaced-keyring grace and the
    /// rotation-refresh retry budget so the grace / retry behavior can be exercised
    /// deterministically without the production ~30s / multi-second timings.
    /// </summary>
    /// <param name="ownServiceId">This service's own id.</param>
    /// <param name="client">The fetch seam.</param>
    /// <param name="channel">The rotation channel.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="grace">Displaced-keyring zeroize grace.</param>
    /// <param name="maxRefreshAttempts">Bounded rotation-refresh attempt cap.</param>
    /// <param name="refreshBackoffBase">Per-attempt backoff base.</param>
    /// <returns>A ready-to-serve opener with the injected timings.</returns>
    internal static KeyringBackedPayloadOpener CreateForTesting(
        string ownServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadOpener> logger,
        TimeSpan grace,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase)
        => Create(
            ownServiceId, client, channel, logger, grace, maxRefreshAttempts, refreshBackoffBase);

    private static KeyringBackedPayloadOpener Create(
        string ownServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadOpener> logger,
        TimeSpan grace,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase)
    {
        ownServiceId.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(logger);

        var sealDomain = SealDomainName.For(ownServiceId);

        // Bound the synchronous boot fetch so a connected-but-unresponsive KeyCustodian
        // cannot hang host startup indefinitely (fail-loud on timeout).
        using var startupCts = new CancellationTokenSource(sr_startupFetchTimeout);
        var fetch = client.GetOwnPrivateKeyringAsync(ownServiceId, startupCts.Token)
            .AsTask().GetAwaiter().GetResult();

        if (!fetch.CheckSuccess(out var keyring) || keyring is null)
        {
            var errorCode = fetch.ErrorCode ?? SealingMetrics.Tags.NONE;
            SealingLog.SealOpenerStartupFetchFailed(logger, sealDomain, errorCode);

            throw new InvalidOperationException(
                $"Seal private-keyring startup fetch for domain '{sealDomain}' failed (error "
                + $"'{errorCode}'). The keyring-backed payload opener cannot be constructed "
                + "without an initial private keyring.");
        }

        var refreshRetryOptions = new RetryOptions<D2Result<RecipientPrivateKeyring>>
        {
            MaxAttempts = maxRefreshAttempts,
            BaseDelayMs = (int)refreshBackoffBase.TotalMilliseconds,
        };

        return new KeyringBackedPayloadOpener(
            ownServiceId, client, keyring, channel, logger, grace, refreshRetryOptions);
    }

    private async Task OnRotationAsync(CancellationToken ct)
    {
        // Bounded, transient-classified retry: a permanent auth/validation failure
        // short-circuits immediately, a transient failure backs off up to the cap.
        var fetch = await RetryHelper.RetryD2ResultAsync(
            (_, token) => r_client.GetOwnPrivateKeyringAsync(r_ownServiceId, token),
            r_refreshRetryOptions,
            ct).ConfigureAwait(false);

        if (fetch.CheckSuccess(out var keyring) && keyring is not null)
        {
            SwapTo(keyring);
            SealingMetrics.RecordRotationHotSwap(r_sealDomain);
            SealingLog.SealOpenerKeyringRefreshSucceeded(r_logger, r_sealDomain);

            return;
        }

        // Terminal: keep serving the current keyring (a later rotation / restart re-drives).
        var errorCode = fetch.ErrorCode ?? SealingMetrics.Tags.NONE;
        SealingMetrics.RecordRefreshFailure(r_sealDomain, errorCode);
        SealingLog.SealKeyringRefreshFailed(r_logger, r_sealDomain, errorCode);
    }

    private void SwapTo(RecipientPrivateKeyring newKeyring)
    {
        var replacement = new Holder(newKeyring, new PayloadOpener(newKeyring));
        var displaced = Interlocked.Exchange(ref _holder, replacement);

        // Dispose racing this swap: DisposeAsync sets _disposed before it snapshots and
        // disposes the holder, so re-check AFTER the atomic exchange (identical ordering to
        // the symmetric twin — exactly one thread zeroizes the replacement).
        if (Volatile.Read(ref _disposed) == 1)
        {
            replacement.Keyring.Dispose();
            displaced.Keyring.Dispose();

            return;
        }

        ScheduleGraceDispose(displaced.Keyring);
    }

    private void ScheduleGraceDispose(RecipientPrivateKeyring displaced)
    {
        // Racing a dispose: zeroize immediately rather than orphan a grace task.
        if (Volatile.Read(ref _disposed) == 1)
        {
            displaced.Dispose();

            return;
        }

        var pending = new PendingDispose(displaced, r_grace, r_pending);
        r_pending[pending] = 0;
        pending.Begin();
    }

    private sealed record Holder(RecipientPrivateKeyring Keyring, IPayloadOpener Opener);

    /// <summary>
    /// A displaced private keyring awaiting its grace-delayed zeroize (the private twin of the
    /// symmetric wrapper's grace machinery — zeroized exactly once, idempotent).
    /// </summary>
    private sealed class PendingDispose : IDisposable
    {
        private readonly RecipientPrivateKeyring r_keyring;
        private readonly TimeSpan r_grace;
        private readonly ConcurrentDictionary<PendingDispose, byte> r_registry;
        private readonly CancellationTokenSource r_cts = new();

        public PendingDispose(
            RecipientPrivateKeyring keyring,
            TimeSpan grace,
            ConcurrentDictionary<PendingDispose, byte> registry)
        {
            r_keyring = keyring;
            r_grace = grace;
            r_registry = registry;
        }

        public Task Task { get; private set; } = Task.CompletedTask;

        public void Begin() => Task = Task.Run(RunAsync);

        public void Cancel()
        {
            try
            {
                r_cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already completed + disposed — the keyring is already zeroized.
            }
        }

        public void Dispose() => r_cts.Dispose();

        private async Task RunAsync()
        {
            try
            {
                await Task.Delay(r_grace, r_cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Shutdown mid-grace — fall through to immediate zeroize.
            }
            catch (ObjectDisposedException)
            {
                // A shutdown drain disposed the CTS before this grace task began (the
                // register-before-Begin window): accessing the disposed token throws.
                // Fall through to zeroize the displaced private keyring anyway — it must
                // never be left unzeroized on the drain path.
            }

            r_keyring.Dispose();
            r_registry.TryRemove(this, out _);
            r_cts.Dispose();
        }
    }
}
