// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadSealer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Client.Sealing;

using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Resilience.Retry;
using Microsoft.Extensions.Logging;

/// <summary>
/// A sealed, hot-swappable <see cref="IPayloadSealer"/> capability backed by a recipient
/// service's KeyCustodian PUBLIC seal keyring — the producer twin of
/// <see cref="KeyringBackedPayloadOpener"/>. Holds only wire-public key material and refreshes
/// atomically on a rotation event.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Least-privilege surface.</strong> The public members are <see cref="Seal"/> /
/// <see cref="DisposeAsync"/> only. There is NO <c>Open</c> — a producer can never open any
/// sealed frame, including its own (the capability split is compile-time).
/// </para>
/// <para>
/// <strong>Lazy public-key fetch (deliberately asymmetric to the opener).</strong> A producer
/// host must not fail BOOT because a recipient has not lazily provisioned its sealing key yet,
/// so the first <see cref="Seal"/> triggers the fetch (bounded retry); a failed fetch surfaces
/// as a thrown exception the publisher maps to a retryable publish failure — NEVER a plaintext
/// fallback. Subsequent seals serve the cached keyring.
/// </para>
/// <para>
/// <strong>No zeroize.</strong> A <see cref="RecipientPublicKeyring"/> holds wire-public
/// material, so a rotation simply swaps the holder reference (in-flight seals finish on the
/// prior coherent keyring; GC reclaims it) — no grace-dispose / zeroize machinery is needed.
/// </para>
/// </remarks>
public sealed class KeyringBackedPayloadSealer : IPayloadSealer, IAsyncDisposable
{
    private const int _REFRESH_MAX_ATTEMPTS = 3;
    private static readonly TimeSpan sr_refreshBackoffBase = TimeSpan.FromSeconds(2);

    // The lazy first fetch is bounded so a connected-but-unresponsive KeyCustodian cannot hang
    // a publish thread indefinitely (fail-loud → retryable publish failure on expiry).
    private static readonly TimeSpan sr_lazyFetchTimeout = TimeSpan.FromSeconds(30);

    private readonly string r_sealDomain;
    private readonly string r_recipientServiceId;
    private readonly ISealingClient r_client;
    private readonly IRotationEventChannel r_channel;
    private readonly ILogger<KeyringBackedPayloadSealer> r_logger;
    private readonly RetryOptions<D2Result<RecipientPublicKeyring>> r_refreshRetryOptions;
    private readonly Lock r_initLock = new();
    private readonly TimeSpan r_lazyFetchTimeout;

    private Holder? _holder;
    private IAsyncDisposable? _subscription;
    private int _disposed;

    private KeyringBackedPayloadSealer(
        string recipientServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadSealer> logger,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase,
        TimeSpan lazyFetchTimeout)
    {
        r_recipientServiceId = recipientServiceId;
        r_sealDomain = SealDomainName.For(recipientServiceId);
        r_client = client;
        r_channel = channel;
        r_logger = logger;
        r_lazyFetchTimeout = lazyFetchTimeout;
        r_refreshRetryOptions = new RetryOptions<D2Result<RecipientPublicKeyring>>
        {
            MaxAttempts = maxRefreshAttempts,
            BaseDelayMs = (int)refreshBackoffBase.TotalMilliseconds,
        };
    }

    /// <inheritdoc />
    public byte[] Seal(ReadOnlySpan<byte> plaintext)
    {
        var holder = Volatile.Read(ref _holder) ?? EnsureInitialized();
        return holder.Sealer.Seal(plaintext);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
            return;

        // Snapshot + clear the subscription under the SAME lock a first-seal subscribes
        // under, so an initialization racing this dispose can never leave a subscription
        // registered after this snapshot: the subscribe and this read are mutually
        // exclusive, so this always observes a subscription the init installed (no leak).
        IAsyncDisposable? subscription;

        lock (r_initLock)
        {
            subscription = _subscription;
            _subscription = null;
        }

        if (subscription is not null)
            await subscription.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Returns the type name only — never any keyring contents.</summary>
    public override string ToString() => nameof(KeyringBackedPayloadSealer);

    /// <summary>
    /// Builds a <see cref="KeyringBackedPayloadSealer"/> for
    /// <paramref name="recipientServiceId"/>. Does NOT fetch — the first <see cref="Seal"/>
    /// lazily fetches the recipient's public keyring (so a producer host never fails boot on a
    /// not-yet-provisioned recipient).
    /// </summary>
    /// <param name="recipientServiceId">The recipient service to seal payloads to.</param>
    /// <param name="client">The (internal) fetch seam.</param>
    /// <param name="channel">The rotation-notification channel (subscribed on first seal).</param>
    /// <param name="logger">Logger.</param>
    /// <returns>A ready-to-serve sealer capability (fetch deferred to first seal).</returns>
    internal static KeyringBackedPayloadSealer Create(
        string recipientServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadSealer> logger)
        => Create(
            recipientServiceId,
            client,
            channel,
            logger,
            _REFRESH_MAX_ATTEMPTS,
            sr_refreshBackoffBase,
            sr_lazyFetchTimeout);

    /// <summary>
    /// Test-only <c>Create</c> overload injecting the rotation-refresh retry budget + the lazy
    /// fetch timeout so the retry / timeout behavior can be exercised deterministically.
    /// </summary>
    /// <param name="recipientServiceId">The recipient service to seal payloads to.</param>
    /// <param name="client">The fetch seam.</param>
    /// <param name="channel">The rotation channel.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="maxRefreshAttempts">Bounded rotation-refresh attempt cap.</param>
    /// <param name="refreshBackoffBase">Per-attempt backoff base.</param>
    /// <param name="lazyFetchTimeout">The bounded lazy first-fetch timeout.</param>
    /// <returns>A ready-to-serve sealer with the injected timings.</returns>
    internal static KeyringBackedPayloadSealer CreateForTesting(
        string recipientServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadSealer> logger,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase,
        TimeSpan lazyFetchTimeout)
        => Create(
            recipientServiceId,
            client,
            channel,
            logger,
            maxRefreshAttempts,
            refreshBackoffBase,
            lazyFetchTimeout);

    private static KeyringBackedPayloadSealer Create(
        string recipientServiceId,
        ISealingClient client,
        IRotationEventChannel channel,
        ILogger<KeyringBackedPayloadSealer> logger,
        int maxRefreshAttempts,
        TimeSpan refreshBackoffBase,
        TimeSpan lazyFetchTimeout)
    {
        recipientServiceId.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(channel);
        ArgumentNullException.ThrowIfNull(logger);

        return new KeyringBackedPayloadSealer(
            recipientServiceId,
            client,
            channel,
            logger,
            maxRefreshAttempts,
            refreshBackoffBase,
            lazyFetchTimeout);
    }

    // Double-checked lazy init: the first seal fetches the public keyring (bounded), installs
    // the holder, and subscribes to rotation. Concurrent first-sealers serialize on the lock
    // and observe the single installed holder. A failed fetch throws (retryable publish
    // failure) and leaves _holder null so the next seal re-attempts.
    private Holder EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        lock (r_initLock)
        {
            var existing = Volatile.Read(ref _holder);

            if (existing is not null)
                return existing;

            using var fetchCts = new CancellationTokenSource(r_lazyFetchTimeout);
            var fetch = r_client.GetPublicKeyringAsync(r_recipientServiceId, fetchCts.Token)
                .AsTask().GetAwaiter().GetResult();

            if (!fetch.CheckSuccess(out var keyring) || keyring is null)
            {
                var errorCode = fetch.ErrorCode ?? SealingMetrics.Tags.NONE;
                SealingMetrics.RecordFetch(r_sealDomain, success: false);
                SealingLog.SealSealerLazyFetchFailed(r_logger, r_sealDomain, errorCode);

                throw new InvalidOperationException(
                    $"Seal public-keyring fetch for domain '{r_sealDomain}' failed (error "
                    + $"'{errorCode}'); the payload cannot be sealed. This is a retryable "
                    + "publish failure — the payload is NEVER shipped as plaintext.");
            }

            var holder = new Holder(new PayloadSealer(keyring));

            // A DisposeAsync that set _disposed during the (bounded) fetch above is now
            // blocked on r_initLock behind this init; there is no point subscribing if
            // disposal has begun — fail loud instead of registering a doomed subscription.
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

            // Subscribe once, on first successful init. The subscribe + the _subscription
            // publish happen under the SAME lock DisposeAsync snapshots under, so a racing
            // dispose always observes (and disposes) this subscription — no leak. A rotation
            // racing this init is fine: the holder is installed before the swap can occur.
            _subscription = r_channel.Subscribe(r_sealDomain, OnRotationAsync);
            Volatile.Write(ref _holder, holder);

            return holder;
        }
    }

    private async Task OnRotationAsync(CancellationToken ct)
    {
        var fetch = await RetryHelper.RetryD2ResultAsync(
            (_, token) => r_client.GetPublicKeyringAsync(r_recipientServiceId, token),
            r_refreshRetryOptions,
            ct).ConfigureAwait(false);

        if (fetch.CheckSuccess(out var keyring) && keyring is not null)
        {
            // Public material: swap the holder reference atomically (no zeroize / grace).
            // In-flight seals finish on the prior coherent keyring; GC reclaims it.
            Volatile.Write(ref _holder, new Holder(new PayloadSealer(keyring)));
            SealingMetrics.RecordRotationHotSwap(r_sealDomain);
            SealingLog.SealKeyringRefreshSucceeded(r_logger, r_sealDomain, keyring.ActiveKid);

            return;
        }

        // Terminal: keep serving the current keyring (a later rotation / restart re-drives).
        var errorCode = fetch.ErrorCode ?? SealingMetrics.Tags.NONE;
        SealingMetrics.RecordRefreshFailure(r_sealDomain, errorCode);
        SealingLog.SealKeyringRefreshFailed(r_logger, r_sealDomain, errorCode);
    }

    // Only the sealer is held — a RecipientPublicKeyring is wire-public (no zeroize / dispose),
    // so unlike the opener's holder the keyring reference is not retained past construction.
    private sealed record Holder(IPayloadSealer Sealer);
}
