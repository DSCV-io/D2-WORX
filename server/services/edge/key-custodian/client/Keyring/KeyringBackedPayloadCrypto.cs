// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadCrypto.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.Client.Keyring;

using System.Collections.Concurrent;
using System.Linq;
using D2.Shared.Encryption;
using D2.Shared.Resilience.Retry;
using Microsoft.Extensions.Logging;

/// <summary>
/// A sealed, hot-swappable <see cref="IPayloadCrypto"/> capability backed by a
/// KeyCustodian keyring. Holds its key material in-process-memory-only, refreshes
/// atomically on a rotation event, and is the ONLY in-memory keyring holder for its
/// domain.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Least-privilege surface.</strong> The public members are
/// <see cref="Encrypt"/> / <see cref="Decrypt"/> / <see cref="DisposeAsync"/> only — NO
/// member ever returns the keyring or raw key bytes. Raw material sits in a private
/// volatile holder, zeroized on dispose.
/// </para>
/// <para>
/// <strong>Torn-read safety.</strong> The <c>(keyring, crypto)</c> pair is swapped as a
/// single reference via <see cref="Interlocked.Exchange{T}(ref T, T)"/>, so an
/// encrypt/decrypt reader can never observe a torn pair and two concurrent rotations can
/// never orphan a displaced holder (each swap returns the true predecessor).
/// </para>
/// <para>
/// <strong>Displaced-keyring disposal.</strong> A swapped-out keyring is zeroized after a
/// ~30s grace (see <see cref="sr_keyringDisposeGrace"/>) scheduled off-thread so it never
/// blocks the rotation-callback thread. In-flight operations against the displaced keyring
/// complete within the grace; <see cref="DisposeAsync"/> drains and force-zeroizes any
/// holder still inside its grace at shutdown.
/// </para>
/// </remarks>
public sealed class KeyringBackedPayloadCrypto : IPayloadCrypto, IAsyncDisposable
{
    // Bounded, handler-side rotation-refresh retry. The KeyringRefresh queue dead-letters
    // (never requeues-to-source) on a handler-result failure, so the retry MUST be
    // handler-side: a persistently-failing refetch against a down KeyCustodian can never
    // tight-loop. On budget exhaustion the wrapper keeps serving the current keyring (loud
    // log + refresh-failure metric); the next rotation event or a restart re-drives.
    private const int _REFRESH_MAX_ATTEMPTS = 3;

    // In-flight (en|de)crypts against a just-swapped keyring complete in milliseconds;
    // ~30s bounds unzeroed key-material retention while making a use-after-dispose
    // implausible in practice. An operation that somehow outlives the grace observes
    // ObjectDisposedException — fail-loud and correct (a documented residual).
    private static readonly TimeSpan sr_keyringDisposeGrace = TimeSpan.FromSeconds(30);

    // Base backoff between bounded rotation-refresh attempts (exponential, via RetryHelper).
    private static readonly TimeSpan sr_refreshBackoffBase = TimeSpan.FromSeconds(2);

    // A connected-but-unresponsive KeyCustodian must never hang host startup forever: the
    // synchronous boot fetch is bounded by this timeout and fails loud on expiry.
    private static readonly TimeSpan sr_startupFetchTimeout = TimeSpan.FromSeconds(30);

    private readonly string r_domain;
    private readonly IKeyringClient r_client;
    private readonly ILogger<KeyringBackedPayloadCrypto> r_logger;
    private readonly IAsyncDisposable r_subscription;
    private readonly ConcurrentDictionary<PendingDispose, byte> r_pending = new();
    private readonly TimeSpan r_grace;
    private readonly RetryOptions<D2Result<PayloadCryptoKeyring>> r_refreshRetryOptions;

    private Holder _holder;
    private int _disposed;

    private KeyringBackedPayloadCrypto(
        string domain,
        IKeyringClient client,
        PayloadCryptoKeyring initialKeyring,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadCrypto> logger,
        TimeSpan grace,
        RetryOptions<D2Result<PayloadCryptoKeyring>> refreshRetryOptions)
    {
        r_domain = domain;
        r_client = client;
        r_logger = logger;
        r_grace = grace;
        r_refreshRetryOptions = refreshRetryOptions;
        _holder = new Holder(initialKeyring, new PayloadCrypto(initialKeyring));
        r_subscription = channel.Subscribe(domain, OnRotationAsync);
    }

    /// <inheritdoc />
    public byte[] Encrypt(ReadOnlySpan<byte> plaintext)
        => Volatile.Read(ref _holder).Crypto.Encrypt(plaintext);

    /// <inheritdoc />
    public byte[] Decrypt(ReadOnlySpan<byte> framed)
        => Volatile.Read(ref _holder).Crypto.Decrypt(framed);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Stop new rotations first, then drain + force-zeroize anything still in grace so
        // no keyring is left unzeroized because the host stopped inside the window.
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
    public override string ToString() => nameof(KeyringBackedPayloadCrypto);

    /// <summary>
    /// Builds a <see cref="KeyringBackedPayloadCrypto"/> for <paramref name="domain"/>,
    /// performing the initial keyring fetch synchronously (fail-loud). A serving-before-ready
    /// async warm-up window is worse than a deliberate one-time blocking boot fetch; the
    /// generic host has no ambient <see cref="SynchronizationContext"/> to deadlock. The
    /// boot fetch is bounded by a startup timeout so an unresponsive KeyCustodian cannot
    /// hang host startup indefinitely.
    /// </summary>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="client">The (internal) fetch seam.</param>
    /// <param name="channel">The rotation-notification channel to subscribe.</param>
    /// <param name="logger">Logger.</param>
    /// <returns>A ready-to-serve capability.</returns>
    /// <exception cref="InvalidOperationException">The startup keyring fetch failed.</exception>
    internal static KeyringBackedPayloadCrypto Create(
        string domain,
        IKeyringClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadCrypto> logger)
        => Create(
            domain,
            client,
            channel,
            logger,
            sr_keyringDisposeGrace,
            _REFRESH_MAX_ATTEMPTS,
            sr_refreshBackoffBase);

    /// <summary>
    /// Test-only <c>Create</c> overload that injects the displaced-keyring grace and
    /// the rotation-refresh retry budget so the grace / retry behavior can be exercised
    /// deterministically without waiting the production ~30s / multi-second timings.
    /// </summary>
    /// <param name="domain">The payload key domain.</param>
    /// <param name="client">The fetch seam.</param>
    /// <param name="channel">The rotation channel.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="grace">Displaced-keyring zeroize grace.</param>
    /// <param name="maxRefreshAttempts">Bounded rotation-refresh attempt cap.</param>
    /// <param name="refreshBackoffBase">Per-attempt backoff base.</param>
    /// <returns>A ready-to-serve capability with the injected timings.</returns>
    internal static KeyringBackedPayloadCrypto CreateForTesting(
        string domain,
        IKeyringClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadCrypto> logger,
        TimeSpan grace,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase)
        => Create(domain, client, channel, logger, grace, maxRefreshAttempts, refreshBackoffBase);

    private static KeyringBackedPayloadCrypto Create(
        string domain,
        IKeyringClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadCrypto> logger,
        TimeSpan grace,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(logger);

        // Bound the synchronous boot fetch so a connected-but-unresponsive KeyCustodian
        // cannot hang host startup indefinitely (fail-loud on timeout).
        using var startupCts = new CancellationTokenSource(sr_startupFetchTimeout);
        var fetch = client.GetKeyringAsync(domain, startupCts.Token)
            .AsTask().GetAwaiter().GetResult();

        if (!fetch.CheckSuccess(out var keyring) || keyring is null)
        {
            var errorCode = fetch.ErrorCode ?? KeyringMetrics.Tags.NONE;
            KeyringLog.KeyringStartupFetchFailed(logger, domain, errorCode);

            throw new InvalidOperationException(
                $"Keyring startup fetch for domain '{domain}' failed (error '{errorCode}'). "
                + "The keyring-backed payload crypto cannot be constructed without an "
                + "initial keyring.");
        }

        // ShouldRetry is left at the default so RetryD2ResultAsync wires its
        // transient-retryable predicate: it retries only ServiceUnavailable / RateLimited,
        // never a permanent auth/validation failure and never an UnhandledException (the
        // retryable-classification seam — an unknown-state result is never auto-retried).
        var refreshRetryOptions = new RetryOptions<D2Result<PayloadCryptoKeyring>>
        {
            MaxAttempts = maxRefreshAttempts,
            BaseDelayMs = (int)refreshBackoffBase.TotalMilliseconds,
        };

        return new KeyringBackedPayloadCrypto(
            domain, client, keyring, channel, logger, grace, refreshRetryOptions);
    }

    private async Task OnRotationAsync(CancellationToken ct)
    {
        // Bounded, transient-classified retry (RetryHelper): a permanent auth/validation
        // failure short-circuits immediately, a transient failure backs off up to the cap.
        var fetch = await RetryHelper.RetryD2ResultAsync(
            (_, token) => r_client.GetKeyringAsync(r_domain, token),
            r_refreshRetryOptions,
            ct).ConfigureAwait(false);

        if (fetch.CheckSuccess(out var keyring) && keyring is not null)
        {
            SwapTo(keyring);
            KeyringMetrics.RecordRotationHotSwap(r_domain);
            KeyringLog.KeyringRefreshSucceeded(r_logger, r_domain, keyring.ActiveKid);

            return;
        }

        // Terminal: keep serving the current keyring (a later rotation / restart re-drives).
        var errorCode = fetch.ErrorCode ?? KeyringMetrics.Tags.NONE;
        KeyringMetrics.RecordRefreshFailure(r_domain, errorCode);
        KeyringLog.KeyringRefreshFailed(r_logger, r_domain, errorCode);
    }

    private void SwapTo(PayloadCryptoKeyring newKeyring)
    {
        var replacement = new Holder(newKeyring, new PayloadCrypto(newKeyring));
        var displaced = Interlocked.Exchange(ref _holder, replacement);

        // Dispose racing this swap: DisposeAsync sets _disposed before it snapshots and
        // disposes the holder, so re-check AFTER the atomic exchange. If dispose has begun,
        // the replacement just installed may never be observed by DisposeAsync's final
        // dispose — zeroize it (and the displaced one) here. The interlocked exchange plus
        // this ordered re-check guarantee exactly one thread zeroizes the replacement.
        if (Volatile.Read(ref _disposed) == 1)
        {
            replacement.Keyring.Dispose();
            displaced.Keyring.Dispose();

            return;
        }

        ScheduleGraceDispose(displaced.Keyring);
    }

    private void ScheduleGraceDispose(PayloadCryptoKeyring displaced)
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

    private sealed record Holder(PayloadCryptoKeyring Keyring, IPayloadCrypto Crypto);

    /// <summary>
    /// A displaced keyring awaiting its grace-delayed zeroize. The delay runs off-thread;
    /// disposal is triggered by the grace elapsing OR by a shutdown-time cancel — either
    /// way the keyring is zeroized exactly once (idempotent).
    /// </summary>
    private sealed class PendingDispose : IDisposable
    {
        private readonly PayloadCryptoKeyring r_keyring;
        private readonly TimeSpan r_grace;
        private readonly ConcurrentDictionary<PendingDispose, byte> r_registry;
        private readonly CancellationTokenSource r_cts = new();

        public PendingDispose(
            PayloadCryptoKeyring keyring,
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

            r_keyring.Dispose();
            r_registry.TryRemove(this, out _);

            // Dispose the CTS on the normal grace-expiry path too (not only the shutdown
            // drain), so one CTS is not leaked per rotation. Idempotent with the drain's
            // Dispose(), which only runs after this task has already been awaited.
            r_cts.Dispose();
        }
    }
}
