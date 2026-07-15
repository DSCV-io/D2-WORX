// -----------------------------------------------------------------------
// <copyright file="KeyringBackedPayloadOpenerTests.cs" company="DCSV">
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
/// Unit coverage for <see cref="KeyringBackedPayloadOpener"/> — the sealed private-keyring
/// hot-swap opener: fail-loud startup, atomic rotation swap + overlap open, bounded
/// keep-serving-current refresh failure, grace-delayed off-thread zeroize, and the
/// least-privilege public surface (Open only — no Seal).
/// </summary>
public sealed class KeyringBackedPayloadOpenerTests
{
    private static readonly TimeSpan sr_shortGrace = TimeSpan.FromMilliseconds(60);
    private static readonly TimeSpan sr_tinyBackoff = TimeSpan.FromMilliseconds(1);

    [Fact]
    public void Create_KeyringUnavailable_ThrowsFailLoud()
    {
        var client = FakeSealingClient.PrivateAlwaysFails(
            D2Result<RecipientPrivateKeyring>.ServiceUnavailable());

        var act = () => KeyringBackedPayloadOpener.Create(
            SealingTestFixtures.FIXTURE_SERVICE_ID,
            client,
            NewChannel(),
            NullLogger<KeyringBackedPayloadOpener>.Instance);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task Open_RoundTripsSealedFrame()
    {
        await using var opener = NewOpener(SingleKidThenRotated(), NewChannel());
        var frame = SealingTestFixtures.Seal(
            "payload"u8.ToArray(), SealingTestFixtures.SingleKidPublicKeyring());

        Encoding.UTF8.GetString(opener.Open(frame)).Should().Be("payload");
    }

    [Fact]
    public async Task DisposeAsync_Idempotent()
    {
        var opener = NewOpener(SingleKidThenRotated(), NewChannel());

        await opener.DisposeAsync();
        var act = async () => await opener.DisposeAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task OpenAfterDispose_Throws()
    {
        var opener = NewOpener(SingleKidThenRotated(), NewChannel());
        var frame = SealingTestFixtures.Seal(
            "x"u8.ToArray(), SealingTestFixtures.SingleKidPublicKeyring());
        await opener.DisposeAsync();

        var act = () => opener.Open(frame);

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task Rotation_SwapsToNewKeyring_AndStillOpensOldKidFrame()
    {
        var channel = NewChannel();
        await using var opener = NewOpener(SingleKidThenRotated(), channel);

        var oldFrame = SealingTestFixtures.Seal(
            "before"u8.ToArray(), SealingTestFixtures.SingleKidPublicKeyring());

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);

        // A frame under the NEW active kid opens against the rotated (kid1+kid2) keyring...
        var newFrame = SealingTestFixtures.Seal(
            "after"u8.ToArray(), SealingTestFixtures.RotatedPublicKeyring());
        Encoding.UTF8.GetString(opener.Open(newFrame)).Should().Be("after");

        // ...and the pre-rotation (kid1) frame still opens (active+retiring overlap).
        Encoding.UTF8.GetString(opener.Open(oldFrame)).Should().Be("before");
    }

    [Fact]
    public async Task RotationEventForDifferentDomain_DoesNotSwap()
    {
        var channel = NewChannel();
        var client = SingleKidThenRotated();
        await using var opener = NewOpener(client, channel);

        await channel.DispatchAsync("seal:some-other-service", CancellationToken.None);

        // No refetch happened for a non-matching domain — only the boot fetch.
        client.PrivateCallCount.Should().Be(1);
    }

    [Fact]
    public async Task RefreshFetchFailsThenRecovers_WithinRetryCap_SwapsToNewKeyring()
    {
        // call 1 (boot) succeeds; call 2 (attempt 1) fails; call 3 (attempt 2) succeeds.
        var client = new FakeSealingClient(privateResponder: i => i switch
        {
            1 => D2Result<RecipientPrivateKeyring>.Ok(
                SealingTestFixtures.SingleKidPrivateKeyring()),
            2 => D2Result<RecipientPrivateKeyring>.ServiceUnavailable(),
            _ => D2Result<RecipientPrivateKeyring>.Ok(
                SealingTestFixtures.RotatedPrivateKeyring()),
        });
        var channel = NewChannel();
        await using var opener = NewOpener(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);

        // A kid2 frame now opens — the swap to the rotated keyring succeeded within the cap.
        var frame = SealingTestFixtures.Seal(
            "x"u8.ToArray(), SealingTestFixtures.RotatedPublicKeyring());
        Encoding.UTF8.GetString(opener.Open(frame)).Should().Be("x");
    }

    [Fact]
    public async Task RefreshFetchFailsPersistently_RetryCapExhausted_KeepsServingCurrent()
    {
        var client = new FakeSealingClient(privateResponder: i => i == 1
            ? D2Result<RecipientPrivateKeyring>.Ok(SealingTestFixtures.SingleKidPrivateKeyring())
            : D2Result<RecipientPrivateKeyring>.ServiceUnavailable());
        var channel = NewChannel();
        await using var opener = NewOpener(client, channel, maxAttempts: 2);

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);

        // Bounded: boot (1) + exactly 2 retry attempts (2, 3). No tight loop.
        client.PrivateCallCount.Should().Be(3);

        // Still serving the current (kid1) keyring — a kid1 frame round-trips.
        var frame = SealingTestFixtures.Seal(
            "still-here"u8.ToArray(), SealingTestFixtures.SingleKidPublicKeyring());
        Encoding.UTF8.GetString(opener.Open(frame)).Should().Be("still-here");
    }

    [Fact]
    public async Task RefreshDeniedByAuthority_NotRetried_KeepsServingCurrent()
    {
        var client = new FakeSealingClient(privateResponder: i => i == 1
            ? D2Result<RecipientPrivateKeyring>.Ok(SealingTestFixtures.SingleKidPrivateKeyring())
            : D2Result<RecipientPrivateKeyring>.Forbidden());
        var channel = NewChannel();
        await using var opener = NewOpener(client, channel, maxAttempts: 3);

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);

        // boot (1) + exactly ONE rotation attempt (2): a permanent failure is not retried.
        client.PrivateCallCount.Should().Be(2);
    }

    [Fact]
    public async Task DisplacedKeyring_ZeroizedAfterGraceWindow()
    {
        RecipientPrivateKeyring? displaced = null;
        var client = new FakeSealingClient(privateResponder: i =>
        {
            var keyring = i == 1
                ? SealingTestFixtures.SingleKidPrivateKeyring()
                : SealingTestFixtures.RotatedPrivateKeyring();
            if (i == 1)
                displaced = keyring;

            return D2Result<RecipientPrivateKeyring>.Ok(keyring);
        });
        var channel = NewChannel();
        await using var opener = NewOpener(client, channel);

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);
        displaced!.ToString().Should().NotContain("disposed");

        await Task.Delay(sr_shortGrace + TimeSpan.FromMilliseconds(300));

        displaced.ToString().Should().Contain("disposed");
    }

    [Fact]
    public async Task DisposeAsync_ShutdownMidGraceWindow_ZeroizesPendingDisplaced()
    {
        RecipientPrivateKeyring? displaced = null;
        var client = new FakeSealingClient(privateResponder: i =>
        {
            var keyring = i == 1
                ? SealingTestFixtures.SingleKidPrivateKeyring()
                : SealingTestFixtures.RotatedPrivateKeyring();
            if (i == 1)
                displaced = keyring;

            return D2Result<RecipientPrivateKeyring>.Ok(keyring);
        });
        var channel = NewChannel();
        var opener = NewOpener(client, channel, grace: TimeSpan.FromMinutes(5));

        await channel.DispatchAsync(SealingTestFixtures.SEAL_DOMAIN, CancellationToken.None);
        displaced!.ToString().Should().NotContain("disposed");

        await opener.DisposeAsync();

        displaced.ToString().Should().Contain("disposed");
    }

    [Fact]
    public void PublicSurface_ExposesOnlyOpenDispose_NeverSeal()
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        typeof(KeyringBackedPayloadOpener).GetProperties(flags).Should().BeEmpty();

        var methods = typeof(KeyringBackedPayloadOpener).GetMethods(flags);
        methods.Should().NotContain(m => m.ReturnType == typeof(RecipientPrivateKeyring));
        methods.Select(m => m.Name)
            .Should().BeSubsetOf(["Open", "DisposeAsync", "ToString"]);

        // Structural capability split: an opener can never seal.
        methods.Select(m => m.Name).Should().NotContain("Seal");
    }

    private static RabbitMqRotationEventChannel NewChannel()
        => new(NullLogger<RabbitMqRotationEventChannel>.Instance);

    private static FakeSealingClient SingleKidThenRotated()
        => new(privateResponder: i => D2Result<RecipientPrivateKeyring>.Ok(
            i == 1
                ? SealingTestFixtures.SingleKidPrivateKeyring()
                : SealingTestFixtures.RotatedPrivateKeyring()));

    private static KeyringBackedPayloadOpener NewOpener(
        FakeSealingClient client,
        RabbitMqRotationEventChannel channel,
        int maxAttempts = 3,
        TimeSpan? grace = null)
        => KeyringBackedPayloadOpener.CreateForTesting(
            SealingTestFixtures.FIXTURE_SERVICE_ID,
            client,
            channel,
            NullLogger<KeyringBackedPayloadOpener>.Instance,
            grace ?? sr_shortGrace,
            maxAttempts,
            sr_tinyBackoff);
}
