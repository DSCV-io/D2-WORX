// -----------------------------------------------------------------------
// <copyright file="GrpcKeyringClientTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Client.Keyring;

using System.Net;
using System.Text;
using AwesomeAssertions;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using global::D2.Services.Protos.KeyCustodian.V2Alpha;
using global::Grpc.Core;
using Google.Protobuf;
using Xunit;
using ProtoGetKeyringOutput = global::D2.Services.Protos.KeyCustodian.V2Alpha.GetKeyringOutput;
using ProtoKeyringEntry = global::D2.Services.Protos.KeyCustodian.V2Alpha.KeyringEntry;

/// <summary>
/// Unit coverage for <see cref="GrpcKeyringClient"/> — the cross-process fetch source —
/// driven through a fake <see cref="CallInvoker"/> so the full envelope + reply mapping is
/// exercised without a server or socket.
/// </summary>
public sealed class GrpcKeyringClientTests
{
    [Fact]
    public async Task GetKeyring_Success_ReturnsUsableKeyring()
    {
        var client = Build(KeyringTestFixtures.Reply(
            D2Result.Ok(), KeyringTestFixtures.WellFormedOutput()));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.ActiveKid.Should().Be(KeyringTestFixtures.KID_ONE);

        // The keyring round-trips a payload — proof it is genuinely usable.
        var crypto = new PayloadCrypto(result.Data);
        var frame = crypto.Encrypt("hello"u8);
        Encoding.UTF8.GetString(crypto.Decrypt(frame)).Should().Be("hello");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetKeyring_NullOrWhitespaceDomain_ReturnsValidationFailed(string? domain)
    {
        var client = Build(KeyringTestFixtures.Reply(
            D2Result.Ok(), KeyringTestFixtures.WellFormedOutput()));

        var result = await client.GetKeyringAsync(domain!);

        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetKeyring_ServerDeniesAuthority_SurfacesForbiddenCode()
    {
        var client = Build(KeyringTestFixtures.Reply(
            D2Result.Forbidden(errorCode: "KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED")));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        result.ErrorCode.Should().Be("KEYCUSTODIAN_KEYRING_DOMAIN_NOT_AUTHORIZED");
        result.Data.Should().BeNull();
    }

    [Fact]
    public async Task GetKeyring_ServerUnavailable_ReturnsServiceUnavailable()
    {
        var invoker = FakeKeyringCallInvoker.Faults(
            new RpcException(new Status(StatusCode.Unavailable, "kc down")));
        var client = new GrpcKeyringClient(
            new KeyCustodianKeyring.KeyCustodianKeyringClient(invoker));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Failed.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetKeyring_EmptyAadContext_ReturnsFailureNotThrow()
    {
        var output = KeyringTestFixtures.WellFormedOutput();
        output.AadContext = ByteString.Empty;

        var client = Build(KeyringTestFixtures.Reply(D2Result.Ok(), output));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task GetKeyring_ActiveKidMissingFromEntries_ReturnsFailure()
    {
        var output = KeyringTestFixtures.WellFormedOutput();
        output.ActiveKid = "kid-not-in-entries";

        var client = Build(KeyringTestFixtures.Reply(D2Result.Ok(), output));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task GetKeyring_WrongKeyLength_ReturnsFailure()
    {
        var output = new ProtoGetKeyringOutput
        {
            ActiveKid = KeyringTestFixtures.KID_ONE,
            AadContext = ByteString.CopyFrom(
                KeyringTestFixtures.AadFor(KeyringTestFixtures.FIXTURE_DOMAIN)),
        };
        output.Entries.Add(new ProtoKeyringEntry
        {
            Kid = KeyringTestFixtures.KID_ONE,
            KeyBytes = ByteString.CopyFrom(new byte[16]),
        });

        var client = Build(KeyringTestFixtures.Reply(D2Result.Ok(), output));

        var result = await client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN);

        result.Failed.Should().BeTrue();
    }

    [Fact]
    public async Task GetKeyring_ConcurrentCallers_EachGetsUsableKeyring()
    {
        var client = Build(KeyringTestFixtures.Reply(
            D2Result.Ok(), KeyringTestFixtures.WellFormedOutput()));

        var tasks = Enumerable.Range(0, 16)
            .Select(_ => client.GetKeyringAsync(KeyringTestFixtures.FIXTURE_DOMAIN).AsTask())
            .ToArray();

        var results = await Task.WhenAll(tasks);

        results.Should().OnlyContain(r => r.Success && r.Data != null);
    }

    private static GrpcKeyringClient Build(GetKeyringResponse response)
        => new(new KeyCustodianKeyring.KeyCustodianKeyringClient(
            FakeKeyringCallInvoker.Returns(response)));
}
