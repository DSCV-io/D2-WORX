// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadCryptoTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// Unit coverage for <see cref="KeyringBackedPayloadCrypto"/> — the sealed hot-swap
/// capability: fail-loud startup, atomic rotation swap + overlap decrypt, bounded
/// keep-serving-current refresh failure, grace-delayed off-thread zeroize, and the
/// least-privilege public surface.
/// </summary>
public sealed class KeyringBackedPayloadCryptoTests
{
    private static readonly TimeSpan sr_shortGrace = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan sr_tinyBackoff = TimeSpan.FromMilliseconds(1);

    [Fact]
    public void Create_KeyringUnavailable_ThrowsFailLoud()
    {
        var client = FakeKeyringClient.AlwaysFails(
            D2Result<PayloadCryptoKeyring>.ServiceUnavailable());

        var act = () => KeyringBackedPayloadCrypto.Create(
            KeyringTestFixtures.FIXTURE_DOMAIN,
            client,
            NewChannel(),
            NullLogger<KeyringBackedPayloadCrypto>.Instance);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task EncryptDecrypt_RoundTrips()
    {
        await using var crypto = NewCrypto(SingleKidThenRotated(), NewChannel());

        var frame = crypto.Encrypt("payload"u8);
        Encoding.UTF8.GetString(crypto.Decrypt(frame)).Should().Be("payload");
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var crypto = NewCrypto(SingleKidThenRotated(), NewChannel());

        await crypto.DisposeAsync();
        var act = async () => await crypto.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task EncryptAfterDispose_Throws()
    {
        var crypto = NewCrypto(SingleKidThenRotated(), NewChannel());
        await crypto.DisposeAsync();

        var act = () => crypto.Encrypt("x"u8);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Rotation_SwapsToNewActiveKid_AndStillDecryptsOldKidFrame()
    {
        var channel = NewChannel();
        await using var crypto = NewCrypto(SingleKidThenRotated(), channel);

        var oldFrame = crypto.Encrypt("before"u8);
        KeyringTestFixtures.ReadFrameKid(oldFrame).Should().Be(KeyringTestFixtures.KID_ONE);

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);

        var newFrame = crypto.Encrypt("after"u8);
        KeyringTestFixtures.ReadFrameKid(newFrame).Should().Be(KeyringTestFixtures.KID_TWO);

        // Overlap guarantee: the pre-rotation frame still decrypts against the rotated keyring.
        Encoding.UTF8.GetString(crypto.Decrypt(oldFrame)).Should().Be("before");
    }

    [Fact]
    public async Task RotationEventForDifferentDomain_DoesNotSwap()
    {
        var channel = NewChannel();
        await using var crypto = NewCrypto(SingleKidThenRotated(), channel);

        await channel.DispatchAsync("some-other-domain", CancellationToken.None);

        KeyringTestFixtures.ReadFrameKid(crypto.Encrypt("x"u8))
            .Should().Be(KeyringTestFixtures.KID_ONE);
    }

    [Fact]
    public async Task RefreshFetchFailsThenRecovers_WithinRetryCap_SwapsToNewKeyring()
    {
        // call 1 (ctor) succeeds; call 2 (attempt 1) fails; call 3 (attempt 2) succeeds.
        var client = new FakeKeyringClient(i => i switch
        {
            1 => D2Result<PayloadCryptoKeyring>.Ok(KeyringTestFixtures.SingleKidKeyring()),
            2 => D2Result<PayloadCryptoKeyring>.ServiceUnavailable(),
            _ => D2Result<PayloadCryptoKeyring>.Ok(KeyringTestFixtures.RotatedKeyring()),
        });
        var channel = NewChannel();
        await using var crypto = NewCrypto(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);

        KeyringTestFixtures.ReadFrameKid(crypto.Encrypt("x"u8))
            .Should().Be(KeyringTestFixtures.KID_TWO);
    }

    [Fact]
    public async Task RefreshFetchFailsPersistently_RetryCapExhausted_KeepsServingCurrent()
    {
        var client = new FakeKeyringClient(i => i == 1
            ? D2Result<PayloadCryptoKeyring>.Ok(KeyringTestFixtures.SingleKidKeyring())
            : D2Result<PayloadCryptoKeyring>.ServiceUnavailable());
        var channel = NewChannel();
        await using var crypto = NewCrypto(client, channel, maxAttempts: 2);

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);

        // Bounded: ctor (1) + exactly 2 retry attempts (2, 3). No tight loop.
        client.CallCount.Should().Be(3);

        // Still serving the current keyring (kid1) — a payload round-trips.
        var frame = crypto.Encrypt("still-here"u8);
        Encoding.UTF8.GetString(crypto.Decrypt(frame)).Should().Be("still-here");
        KeyringTestFixtures.ReadFrameKid(frame).Should().Be(KeyringTestFixtures.KID_ONE);
    }

    [Fact]
    public async Task DisplacedKeyring_AliveDuringGrace_ThenZeroizedAfterGraceWindow()
    {
        PayloadCryptoKeyring? displaced = null;
        var client = CapturingClient(k => displaced = k);
        var channel = NewChannel();
        await using var crypto = NewCrypto(client, channel);

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);

        // Within the grace window the displaced keyring is still usable (in-flight ops safe).
        displaced!.ToString().Should().NotContain("disposed");

        await Task.Delay(sr_shortGrace + TimeSpan.FromMilliseconds(300));

        displaced.ToString().Should().Contain("disposed");
    }

    [Fact]
    public async Task DisposeAsync_ShutdownMidGraceWindow_ZeroizesPendingDisplaced()
    {
        PayloadCryptoKeyring? displaced = null;
        var client = CapturingClient(k => displaced = k);
        var channel = NewChannel();
        var crypto = NewCrypto(client, channel, grace: TimeSpan.FromMinutes(5));

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);
        displaced!.ToString().Should().NotContain("disposed"); // grace is 5 min — still pending.

        await crypto.DisposeAsync(); // shutdown mid-grace drains + force-zeroizes.

        displaced.ToString().Should().Contain("disposed");
    }

    [Fact]
    public async Task GraceDelayedDispose_DoesNotBlockRotationCallback()
    {
        var channel = NewChannel();
        await using var crypto = NewCrypto(
            SingleKidThenRotated(), channel, grace: TimeSpan.FromMinutes(5));

        var stopwatch = Stopwatch.StartNew();
        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);
        stopwatch.Stop();

        // The 5-minute grace is scheduled off-thread — dispatch returns near-instantly.
        stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void PublicSurface_ExposesOnlyEncryptDecryptDispose()
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        // No public property (a keyring/keyBytes getter) is exposed at all.
        typeof(KeyringBackedPayloadCrypto).GetProperties(flags).Should().BeEmpty();

        // No public method returns the keyring type; only the capability + dispose exists.
        var methods = typeof(KeyringBackedPayloadCrypto).GetMethods(flags);
        methods.Should().NotContain(m => m.ReturnType == typeof(PayloadCryptoKeyring));
        methods.Select(m => m.Name)
            .Should().BeSubsetOf(["Encrypt", "Decrypt", "DisposeAsync", "ToString"]);
    }

    [Fact]
    public async Task EncryptDuringSwap_NeverObservesTornState()
    {
        // Every swap installs a fresh keyring instance that still carries the same active
        // kid, so any frame stays decryptable — the test isolates the atomic holder swap
        // (a torn read would surface as a NullReference / disposed-keyring crash, never a
        // clean KidNotInKeyring).
        var client = new FakeKeyringClient(
            _ => D2Result<PayloadCryptoKeyring>.Ok(KeyringTestFixtures.SingleKidKeyring()));
        var channel = NewChannel();
        await using var crypto = NewCrypto(client, channel);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // ReSharper disable AccessToDisposedClosure -- await hammer below completes
        // the closure before cts / crypto dispose, which R# can't prove statically.
        var hammer = Task.Run(() =>
        {
            while (!cts.IsCancellationRequested)
            {
                var frame = crypto.Encrypt("t"u8);
                Encoding.UTF8.GetString(crypto.Decrypt(frame)).Should().Be("t");
            }
        });

        // ReSharper restore AccessToDisposedClosure

        while (!cts.IsCancellationRequested)
            await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, cts.Token);

        var act = async () => await hammer;
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RefreshFetchDeniedByAuthority_NotRetried_KeepsServingCurrent()
    {
        // A permanent authority denial must short-circuit the bounded retry immediately —
        // only transient (ServiceUnavailable / RateLimited) failures are retried.
        var client = new FakeKeyringClient(i => i == 1
            ? D2Result<PayloadCryptoKeyring>.Ok(KeyringTestFixtures.SingleKidKeyring())
            : D2Result<PayloadCryptoKeyring>.Forbidden());
        var channel = NewChannel();
        await using var crypto = NewCrypto(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None);

        // ctor (1) + exactly ONE rotation attempt (2): the permanent failure is not retried
        // even though the attempt cap is 3 (a retry-all loop would reach 4 calls).
        client.CallCount.Should().Be(2);
        KeyringTestFixtures.ReadFrameKid(crypto.Encrypt("x"u8))
            .Should().Be(KeyringTestFixtures.KID_ONE);
    }

    [Fact]
    public async Task ConcurrentRotations_EveryDisplacedKeyringIsZeroized()
    {
        // Each rotation fetch hands out a fresh keyring instance we capture. The fake yields
        // before responding and each dispatch runs via Task.Run, so the 32 rotation
        // callbacks resume on pool threads and their SwapTo critical sections genuinely
        // overlap. The atomic swap must return the true predecessor on every rotation so no
        // displaced holder is orphaned; a non-atomic read-modify-write would leak one
        // un-zeroized under this real concurrency.
        var captured = new ConcurrentQueue<PayloadCryptoKeyring>();
        var client = new FakeKeyringClient(
            _ =>
            {
                var keyring = KeyringTestFixtures.SingleKidKeyring();
                captured.Enqueue(keyring);

                return D2Result<PayloadCryptoKeyring>.Ok(keyring);
            },
            yieldBeforeRespond: true);
        var channel = NewChannel();
        var crypto = NewCrypto(client, channel);

        var rotations = Enumerable.Range(0, 32)
            .Select(_ => Task.Run(() => channel.DispatchAsync(
                KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None)))
            .ToArray();

        await Task.WhenAll(rotations);

        // Let every grace-delayed zeroize elapse, then dispose the final live holder.
        await Task.Delay(sr_shortGrace + TimeSpan.FromMilliseconds(400));
        await crypto.DisposeAsync();

        captured.Count.Should().BeGreaterThan(1);
        captured.Should().OnlyContain(k => k.ToString().Contains("disposed"));
    }

    [Fact]
    public async Task RotationDuringDispose_DoesNotThrow_AndLeaksNoKeyring()
    {
        // A rotation racing DisposeAsync must not throw, and the replacement it installs
        // must still be zeroized (the post-swap dispose re-check), never orphaned.
        var captured = new ConcurrentQueue<PayloadCryptoKeyring>();
        var client = new FakeKeyringClient(_ =>
        {
            var keyring = KeyringTestFixtures.SingleKidKeyring();
            captured.Enqueue(keyring);

            return D2Result<PayloadCryptoKeyring>.Ok(keyring);
        });
        var channel = NewChannel();
        var crypto = NewCrypto(client, channel);

        var rotate = Task.Run(() => channel.DispatchAsync(
            KeyringTestFixtures.FIXTURE_DOMAIN, CancellationToken.None));
        var dispose = crypto.DisposeAsync().AsTask();

        var act = async () => await Task.WhenAll(rotate, dispose);
        await act.Should().NotThrowAsync();

        // Any grace-scheduled zeroize elapses; then every installed keyring is zeroized.
        await Task.Delay(sr_shortGrace + TimeSpan.FromMilliseconds(400));
        captured.Should().OnlyContain(k => k.ToString().Contains("disposed"));
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    // ctor keyring is single-kid; every rotation fetch returns the rotated keyring.
    private static FakeKeyringClient SingleKidThenRotated()
        => new(i => D2Result<PayloadCryptoKeyring>.Ok(
            i == 1
                ? KeyringTestFixtures.SingleKidKeyring()
                : KeyringTestFixtures.RotatedKeyring()));

    // Captures the initial (ctor) keyring so a test can assert its displaced disposal.
    private static FakeKeyringClient CapturingClient(Action<PayloadCryptoKeyring> onInitial)
        => new(i =>
        {
            var keyring = i == 1
                ? KeyringTestFixtures.SingleKidKeyring()
                : KeyringTestFixtures.RotatedKeyring();
            if (i == 1)
                onInitial(keyring);

            return D2Result<PayloadCryptoKeyring>.Ok(keyring);
        });

    private static KeyringBackedPayloadCrypto NewCrypto(
        FakeKeyringClient client,
        RabbitMqRotationEventChannel channel,
        int maxAttempts = 3,
        TimeSpan? grace = null)
        => KeyringBackedPayloadCrypto.CreateForTesting(
            KeyringTestFixtures.FIXTURE_DOMAIN,
            client,
            channel,
            NullLogger<KeyringBackedPayloadCrypto>.Instance,
            grace ?? sr_shortGrace,
            maxAttempts,
            sr_tinyBackoff);
}
