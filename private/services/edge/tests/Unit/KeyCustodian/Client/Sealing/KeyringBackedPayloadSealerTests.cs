// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadSealerTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Client.Sealing;

using System.Reflection;
using System.Text;
using AwesomeAssertions;
using D2.Edge.KeyCustodian.Client.Keyring;
using D2.Edge.KeyCustodian.Client.Sealing;
using D2.Shared.Encryption;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit coverage for <see cref="KeyringBackedPayloadSealer"/> — the producer-side public-keyring
/// hot-swap sealer: LAZY first-fetch (no boot fetch, so a producer never fails startup on a
/// not-yet-provisioned recipient), a failed fetch surfacing as a thrown retryable publish
/// failure (never a plaintext fallback), atomic rotation swap, and the least-privilege public
/// surface (Seal only — no Open).
/// </summary>
public sealed class KeyringBackedPayloadSealerTests
{
    private static readonly TimeSpan sr_tinyBackoff = TimeSpan.FromMilliseconds(1);
    private static readonly TimeSpan sr_lazyTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Create_DoesNotFetch_UntilFirstSeal()
    {
        var client = new FakeSealingClient(publicResponder:
            _ => D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));

        await using var sealer = NewSealer(client, NewChannel());

        // Lazy: construction performs NO fetch (a producer host must not fail boot on an
        // unprovisioned recipient).
        client.PublicCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Seal_FirstCall_FetchesThenProducesOpenableFrame()
    {
        var client = new FakeSealingClient(publicResponder:
            _ => D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));
        await using var sealer = NewSealer(client, NewChannel());

        var frame = sealer.Seal("payload"u8);

        client.PublicCallCount.Should().Be(1);
        var opened = new PayloadOpener(SealingTestFixtures.SingleKidPrivateKeyring()).Open(frame);
        Encoding.UTF8.GetString(opened).Should().Be("payload");
    }

    [Fact]
    public async Task Seal_FetchFails_ThrowsRetryable_NeverPlaintext()
    {
        var client = FakeSealingClient.PublicAlwaysFails(
            D2Result<RecipientPublicKeyring>.ServiceUnavailable());
        await using var sealer = NewSealer(client, NewChannel());

        // ReSharper disable once AccessToDisposedClosure -- the throwing seal runs synchronously
        // via Should().Throw() before the await using disposes the sealer.
        var act = () => sealer.Seal("secret"u8);

        // Fail-loud: a failed fetch throws (the publisher maps to a retryable publish
        // failure) — there is NO plaintext fallback path.
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Seal_FetchFailsThenRecovers_RetriesOnNextSeal()
    {
        var client = new FakeSealingClient(publicResponder: i => i == 1
            ? D2Result<RecipientPublicKeyring>.ServiceUnavailable()
            : D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));
        await using var sealer = NewSealer(client, NewChannel());

        // First seal fails (holder stays null → retryable); the next seal re-attempts + succeeds.
        // ReSharper disable once AccessToDisposedClosure -- runs synchronously via Should().Throw().
        var firstAttempt = () => sealer.Seal("x"u8);
        firstAttempt.Should().Throw<InvalidOperationException>();

        var frame = sealer.Seal("x"u8);
        client.PublicCallCount.Should().Be(2);
        var opened = new PayloadOpener(SealingTestFixtures.SingleKidPrivateKeyring()).Open(frame);
        Encoding.UTF8.GetString(opened).Should().Be("x");
    }

    [Fact]
    public async Task Rotation_SwapsToNewActiveKid()
    {
        var client = new FakeSealingClient(publicResponder: i =>
            D2Result<RecipientPublicKeyring>.Ok(
                i == 1
                    ? SealingTestFixtures.SingleKidPublicKeyring()
                    : SealingTestFixtures.RotatedPublicKeyring()));
        var channel = NewChannel();
        await using var sealer = NewSealer(client, channel);

        _ = sealer.Seal("pre"u8); // first seal -> fetch kid1 + subscribe.

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);

        var frame = sealer.Seal("post"u8);

        // Post-rotation the sealer seals under the NEW active kid (kid2): a kid2-bearing
        // private keyring opens it, a kid1-only keyring cannot.
        var openedByBoth = new PayloadOpener(
            SealingTestFixtures.RotatedPrivateKeyring()).Open(frame);
        Encoding.UTF8.GetString(openedByBoth).Should().Be("post");

        var kid1Only = () => new PayloadOpener(
            SealingTestFixtures.SingleKidPrivateKeyring()).Open(frame);
        kid1Only.Should().Throw<Exception>(
            "the frame was sealed under the rotated (kid2) key");
    }

    [Fact]
    public async Task ConcurrentFirstSeals_FetchExactlyOnce()
    {
        var client = new FakeSealingClient(publicResponder:
            _ => D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));
        await using var sealer = NewSealer(client, NewChannel());

        // ReSharper disable once AccessToDisposedClosure -- all 16 seals complete under
        // Task.WhenAll before the await using disposes the sealer.
        var seals = Enumerable.Range(0, 16)
            .Select(_ => Task.Run(() => sealer.Seal("t"u8)))
            .ToArray();

        await Task.WhenAll(seals);

        // Double-checked lazy init: exactly one fetch despite 16 concurrent first-sealers.
        client.PublicCallCount.Should().Be(1);
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var client = new FakeSealingClient(publicResponder:
            _ => D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));
        var sealer = NewSealer(client, NewChannel());
        _ = sealer.Seal("x"u8);

        await sealer.DisposeAsync();
        var act = async () => await sealer.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SealAfterDispose_Throws()
    {
        var client = new FakeSealingClient(publicResponder:
            _ => D2Result<RecipientPublicKeyring>.Ok(SealingTestFixtures.SingleKidPublicKeyring()));
        var sealer = NewSealer(client, NewChannel());
        await sealer.DisposeAsync();

        var act = () => sealer.Seal("x"u8);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task FirstSealRacingDispose_DisposesSubscription_NoLeak()
    {
        // A first seal that is mid-fetch when DisposeAsync runs must not leave the
        // rotation subscription registered on the singleton channel. The fetch is gated
        // so the dispose deterministically races the in-flight init.
        var fetchEntered =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFetch =
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new GatedSealingClient(fetchEntered, releaseFetch.Task);
        var channel = NewChannel();
        var sealer = NewSealer(client, channel);

        // First seal enters EnsureInitialized, holds r_initLock, blocks in the gated fetch.
        var sealTask = Task.Run(() => sealer.Seal("x"u8));
        await fetchEntered.Task;

        // Dispose races the in-flight first seal (blocks on r_initLock behind the init).
        var disposeTask = Task.Run(async () => await sealer.DisposeAsync());

        releaseFetch.SetResult();

        // The seal may throw (re-check saw disposal) or complete — either way no
        // subscription may survive the dispose.
        try
        {
            await sealTask;
        }
        catch (ObjectDisposedException)
        {
            // Expected when the init re-check observed disposal.
        }

        await disposeTask;

        // A leaked rotation subscription would re-fetch on the next dispatch; assert it
        // does NOT (the subscription was disposed under the shared lock).
        var before = client.PublicCallCount;
        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);
        client.PublicCallCount.Should().Be(before);
    }

    [Fact]
    public void PublicSurface_ExposesOnlySealDispose_NeverOpen()
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        typeof(KeyringBackedPayloadSealer).GetProperties(flags).Should().BeEmpty();

        var methods = typeof(KeyringBackedPayloadSealer).GetMethods(flags);
        methods.Should().NotContain(m => m.ReturnType == typeof(RecipientPublicKeyring));
        methods.Select(m => m.Name)
            .Should().BeSubsetOf(["Seal", "DisposeAsync", "ToString"]);

        // Structural capability split: a sealer can never open.
        methods.Select(m => m.Name).Should().NotContain("Open");
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    private static KeyringBackedPayloadSealer NewSealer(
        ISealingClient client,
        RabbitMqRotationEventChannel channel,
        int maxAttempts = 3)
        => KeyringBackedPayloadSealer.CreateForTesting(
            SealingTestFixtures.FIXTURE_SERVICE_ID,
            client,
            channel,
            NullLogger<KeyringBackedPayloadSealer>.Instance,
            maxAttempts,
            sr_tinyBackoff,
            sr_lazyTimeout);

    /// <summary>
    /// A fixture <see cref="ISealingClient"/> whose public fetch signals when it starts
    /// and blocks on a caller-controlled gate, so a test can deterministically race
    /// DisposeAsync against an in-flight first-seal init.
    /// </summary>
    private sealed class GatedSealingClient(
        TaskCompletionSource fetchEntered, Task releaseFetch) : ISealingClient
    {
        private int _publicCalls;

        public int PublicCallCount => Volatile.Read(ref _publicCalls);

        public ValueTask<D2Result<RecipientPrivateKeyring>> GetOwnPrivateKeyringAsync(
            string ownServiceId, CancellationToken ct = default)
            => ValueTask.FromResult(D2Result<RecipientPrivateKeyring>.ServiceUnavailable());

        public async ValueTask<D2Result<RecipientPublicKeyring>> GetPublicKeyringAsync(
            string recipientServiceId, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _publicCalls);
            fetchEntered.TrySetResult();
            await releaseFetch.ConfigureAwait(false);

            return D2Result<RecipientPublicKeyring>.Ok(
                SealingTestFixtures.SingleKidPublicKeyring());
        }
    }
}
