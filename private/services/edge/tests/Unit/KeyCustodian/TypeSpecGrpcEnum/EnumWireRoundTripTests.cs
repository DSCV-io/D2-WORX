// -----------------------------------------------------------------------
// <copyright file="EnumWireRoundTripTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcEnum;

using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using D2.Edge.Tests.TypeSpecGrpcEnum.Generated;
using D2.Services.Protos.EnumFixtures.V1;
using D2.Shared.Result;
using D2.Shared.Utilities.Serialization;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// FixtureKeyKind is emitted in BOTH the gRPC-enum DTO namespace (SignWithKindFixtureInput.g.cs)
// and the in-process enum DTO namespace (EnumFixtureOutput.g.cs). The Level / Status /
// AccountKind aliases pin the in-process DTO-ns enums. FixtureKeyKind is referenced directly
// from the gRPC-enum namespace (where ToWire / ParseFixtureKeyKindWire live) — no alias so
// the fixture-only name is visible at every call site (§7.23).
// SignWithKindFixtureOutput exists as both the DTO record (TypeSpecGrpcEnum.Generated) and
// the Grpc.Tools proto message (D2.Services.Protos.EnumFixtures.V1); pin the DTO.
using AccountKind = D2.Edge.Tests.TypeSpecDto.Generated.FixtureAccountKind;
using Level = D2.Edge.Tests.TypeSpecDto.Generated.FixtureLevel;
using SignWithKindFixtureOutput = D2.Edge.Tests.TypeSpecGrpcEnum.Generated.SignWithKindFixtureOutput;
using Status = D2.Edge.Tests.TypeSpecDto.Generated.FixtureStatus;

/// <summary>
/// Cross-language enum-wire round-trip suite for the TypeSpec-emitted wire enums.
///
/// The single load-bearing claim: the SAME wire string materializes to the SAME
/// enum member across ALL THREE transports — C# JSON (<c>JsonStringEnumConverter</c>
/// via <see cref="SerializerOptions.SR_Web"/>), the proto `string`-field path
/// (the generated <c>SignWithKindFixtureTransportMappers</c>), and TS (the const-object,
/// asserted by <c>enum-wire-round-trip.test.ts</c> driving the SAME shared fixture
/// <c>contracts/enum/enum-parity.fixture.json</c>). An UNKNOWN wire value fails
/// LOUD on every transport — NO silent fallback sentinel:
///   - C# JSON  → <see cref="JsonException"/> at deserialization.
///   - proto    → the mapper returns <c>ValidationFailed</c> (400) and the gRPC
///                service short-circuits WITHOUT invoking the handler.
///   - TS       → the const-object membership lookup misses.
///
/// These exercise the REAL <c>SerializerOptions.SR_Web</c> preset + the REAL
/// generated mappers + the REAL Grpc.Tools-generated proto types — no test doubles.
/// </summary>
public sealed class EnumWireRoundTripTests
{
    private static readonly JsonSerializerOptions sr_fixtureJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    // -------------------------------------------------------------------------
    // P-1..P-4 — C# JSON: each member ⇄ its wire string via the REAL SR_Web preset
    // -------------------------------------------------------------------------

    [Fact]
    public void P1_FixtureKeyKind_BareMember_WireIsMemberName_RoundTrips()
    {
        foreach (var (member, wire) in MembersOf("FixtureKeyKind"))
        {
            var value = ParseFixtureKeyKind(member);

            // Serialize via the REAL SR_Web preset (JsonStringEnumConverter).
            var json = JsonSerializer.Serialize(value, SerializerOptions.SR_Web);
            json.Should().Be($"\"{wire}\"", $"member {member} must serialize to its wire string");

            var back = JsonSerializer.Deserialize<FixtureKeyKind>(json, SerializerOptions.SR_Web);
            back.Should().Be(value, $"wire '{wire}' must deserialize back to {member}");
        }
    }

    [Fact]
    public void P2_Level_ExplicitInt_WireIsMemberNameNotInt()
    {
        // The load-bearing S-2 assertion: an explicit-int enum still serializes
        // as the member NAME string, NEVER the integer backing.
        foreach (var (member, wire) in MembersOf("FixtureLevel"))
        {
            var value = ParseLevel(member);
            var json = JsonSerializer.Serialize(value, SerializerOptions.SR_Web);

            json.Should().Be($"\"{wire}\"", "the wire is the member name, not the int backing");
            json.Should().NotMatch("*0*").And.NotMatch("*5*").And.NotMatch("*10*");
        }

        // Concretely: Level.High → "High", not "10".
        JsonSerializer.Serialize(Level.High, SerializerOptions.SR_Web).Should().Be("\"High\"");
    }

    [Fact]
    public void P3_Status_StringLiteralUnion_LowercaseWire_RoundTrips()
    {
        foreach (var (member, wire) in MembersOf("FixtureStatus"))
        {
            var value = ParseStatus(member);
            var json = JsonSerializer.Serialize(value, SerializerOptions.SR_Web);

            json.Should().Be($"\"{wire}\"");
            JsonSerializer.Deserialize<Status>(json, SerializerOptions.SR_Web).Should().Be(value);
        }
    }

    [Fact]
    public void P4_AccountKind_EnumMemberLiteral_ThirdPartyWiresAsHyphenated()
    {
        // The [EnumMember(Value="third-party")] parity proof: the wire is the
        // literal "third-party", NOT the PascalCase member name "ThirdParty".
        JsonSerializer.Serialize(AccountKind.ThirdParty, SerializerOptions.SR_Web)
            .Should().Be("\"third-party\"");
        JsonSerializer.Serialize(AccountKind.Internal, SerializerOptions.SR_Web)
            .Should().Be("\"internal\"");

        JsonSerializer.Deserialize<AccountKind>("\"third-party\"", SerializerOptions.SR_Web)
            .Should().Be(AccountKind.ThirdParty);
    }

    // -------------------------------------------------------------------------
    // AD-1 (C# JSON half) — UNKNOWN wire value → JsonException (NO fallback)
    // -------------------------------------------------------------------------

    [Fact]
    public void AD1_UnknownWireValue_JsonDeserialization_ThrowsJsonException()
    {
        // The headline adversarial case: an unknown wire string is REJECTED — no
        // silent map to a default / Unknown sentinel (there is no such sentinel).
        foreach (var unknown in UnknownValuesOf("FixtureKeyKind"))
        {
            // Case-insensitive accepted forms ("rsa" → Rsa, "RSA" → Rsa) are NOT
            // adversarial for JsonStringEnumConverter — skip those; assert the
            // genuinely-unknown values throw.
            if (IsKnownIgnoringCase("FixtureKeyKind", unknown))
                continue;

            var act = () => JsonSerializer.Deserialize<FixtureKeyKind>($"\"{unknown}\"", SerializerOptions.SR_Web);
            act.Should().Throw<JsonException>($"'{unknown}' is not a FixtureKeyKind wire value");
        }
    }

    [Fact]
    public void AD2_CaseInsensitivity_DocumentedDivergence_CSharpAcceptsTsRejects()
    {
        // JsonStringEnumConverter is case-INSENSITIVE by default — "rsa"/"RSA"
        // deserialize to FixtureKeyKind.Rsa in C#. The TS const-object is case-SENSITIVE
        // (exact key only). This is a documented cross-language divergence (like
        // the C# JsonException/TS-RangeError split for unknown enum values) — pinned here, surfaced in
        // VALIDATION.md, NOT silently reconciled. The TS half asserts membership
        // misses for the same values.
        JsonSerializer.Deserialize<FixtureKeyKind>("\"rsa\"", SerializerOptions.SR_Web).Should().Be(FixtureKeyKind.Rsa);
        JsonSerializer.Deserialize<FixtureKeyKind>("\"RSA\"", SerializerOptions.SR_Web).Should().Be(FixtureKeyKind.Rsa);
    }

    [Fact]
    public void AD3_NullForRequiredEnum_ThrowsOrIsRejected()
    {
        // A JSON null for a non-nullable enum value type fails to deserialize.
        var act = () => JsonSerializer.Deserialize<FixtureKeyKind>("null", SerializerOptions.SR_Web);
        act.Should().Throw<JsonException>("null is not a valid value for a non-nullable enum");
    }

    [Fact]
    public void AD4_EmptyOrWhitespaceWireValue_ThrowsJsonException()
    {
        foreach (var bad in new[] { "\"\"", "\" \"" })
        {
            var act = () => JsonSerializer.Deserialize<Status>(bad, SerializerOptions.SR_Web);
            act.Should().Throw<JsonException>($"{bad} is not a Status wire value");
        }
    }

    // -------------------------------------------------------------------------
    // AD-1 (proto-mapper half) — the load-bearing fail-loud parse
    // -------------------------------------------------------------------------

    [Fact]
    public void ProtoMapper_ParseFixtureKeyKindWire_KnownValue_ReturnsOk()
    {
        foreach (var (member, wire) in MembersOf("FixtureKeyKind"))
        {
            var result = string.ParseFixtureKeyKindWire(wire);

            result.Success.Should().BeTrue($"'{wire}' is a valid FixtureKeyKind wire value");
            result.Data.Should().Be(ParseFixtureKeyKind(member));
        }
    }

    [Theory]
    [InlineData("Quantum")]
    [InlineData("rsa")]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void ProtoMapper_ParseFixtureKeyKindWire_UnknownValue_FailsLoudValidationFailed(string? unknown)
    {
        // The proto-string bridge is STRICT: an unknown wire value → ValidationFailed
        // (400), NOT a silent fallback. This is the inbound half of the cross-language
        // fail-loud contract (the C# JSON half throws JsonException above; the TS half
        // misses const-object membership).
        var result = string.ParseFixtureKeyKindWire(unknown);

        result.Success.Should().BeFalse($"'{unknown ?? "<null>"}' is not a FixtureKeyKind wire value");
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void ProtoMapper_ToWire_RoundTripsWithParse()
    {
        // ToWire (outbound) ∘ ParseFixtureKeyKindWire (inbound) is the identity over the
        // closed enum — proving the outbound + inbound bridges agree.
        foreach (var kind in new[] { FixtureKeyKind.Rsa, FixtureKeyKind.Aes, FixtureKeyKind.Secret })
        {
            var wire = kind.ToWire();
            string.ParseFixtureKeyKindWire(wire).Data.Should().Be(kind);
        }
    }

    // -------------------------------------------------------------------------
    // gRPC end-to-end — the proto string ⇄ DTO enum bridge over a real channel
    // -------------------------------------------------------------------------

    [Fact]
    public async Task Grpc_SignWithKind_ValidEnumWire_HandlerReceivesParsedEnum()
    {
        var fake = new FakeSignWithKindFixtureHandler(
            D2Result<SignWithKindFixtureOutput?>.Ok(new SignWithKindFixtureOutput("sig==", FixtureKeyKind.Secret)));

        using var host = await BuildHost(fake);
        using var channel = CreateChannel(host);
        var client = new EnumFixturesSigner.EnumFixturesSignerClient(channel);

        // The proto carries key_kind = "Aes" (the string wire form).
        var reply = await client.SignWithKindFixtureAsync(new SignWithKindFixtureRequest
        {
            Kid = "k1",
            KeyKind = "Aes",
        });

        reply.Result.Success.Should().BeTrue();
        reply.Data.Signature.Should().Be("sig==");

        // THE inbound bridge assertion: the request proto string "Aes" parsed back to the C# enum.
        fake.CallCount.Should().Be(1);
        fake.LastInput!.Kid.Should().Be("k1");
        fake.LastInput.KeyKind.Should().Be(FixtureKeyKind.Aes);

        // THE outbound bridge assertion: the C# response enum FixtureKeyKind.Secret is serialized to
        // the proto `string key_kind` wire form on the response (server ToWire path).
        reply.Data.KeyKind.Should().Be("Secret");
    }

    [Fact]
    public async Task Grpc_SignWithKind_UnknownEnumWire_FailsLoud_HandlerNeverInvoked()
    {
        // The load-bearing inbound-fail-loud proof: an unknown key_kind wire value
        // → the transport mapper returns ValidationFailed and the service short-
        // circuits to the envelope WITHOUT delegating to the handler. The gRPC call
        // SUCCEEDS at the transport layer (StatusCode.OK); the 400 rides the envelope.
        var fake = new FakeSignWithKindFixtureHandler(
            D2Result<SignWithKindFixtureOutput?>.Ok(new SignWithKindFixtureOutput("should-not-be-returned", FixtureKeyKind.Rsa)));

        using var host = await BuildHost(fake);
        using var channel = CreateChannel(host);
        var client = new EnumFixturesSigner.EnumFixturesSignerClient(channel);

        var reply = await client.SignWithKindFixtureAsync(new SignWithKindFixtureRequest
        {
            Kid = "k1",
            KeyKind = "Quantum", // not a FixtureKeyKind wire value
        });

        reply.Result.Success.Should().BeFalse("an unknown enum wire value is a business validation failure");
        reply.Result.StatusCode.Should().Be(400);
        reply.Data.Should().BeNull("no data is produced when the request enum is invalid");

        // The handler is NEVER reached — the mapper rejected the request first.
        fake.CallCount.Should().Be(0);
    }

    // -------------------------------------------------------------------------
    // Client RESPONSE-enum parse — the inbound CLIENT analogue of the server
    // request parse (symmetric: gRPC enum surface complete for request AND
    // response). The generated SignWithKindFixtureClientMappers.ToSignWithKindFixtureOutput()
    // parses the proto `string key_kind` back to the C# enum, failing loud on an
    // unknown wire value (ValidationFailed, NO fallback sentinel).
    // -------------------------------------------------------------------------

    [Fact]
    public void ClientMapper_ToSignWithKindFixtureOutput_ValidResponseEnum_ParsesToDtoEnum()
    {
        foreach (var (member, wire) in MembersOf("FixtureKeyKind"))
        {
            var protoData = new global::D2.Services.Protos.EnumFixtures.V1.SignWithKindFixtureOutput
            {
                Signature = "sig==",
                KeyKind = wire,
            };

            // The client response mapper is invoked via its declaring static class
            // (the C# 14 extension member lowers to a static method) so the file does
            // NOT import the Clients namespace — that would make the duplicated
            // ToWire / ParseFixtureKeyKindWire helpers (server + client mappers) ambiguous.
            var result = MapClientOutput(protoData);

            result.Success.Should().BeTrue($"'{wire}' is a valid response FixtureKeyKind wire value");
            result.Data!.Signature.Should().Be("sig==");
            result.Data.KeyKind.Should().Be(ParseFixtureKeyKind(member));
        }
    }

    [Theory]
    [InlineData("Quantum")]
    [InlineData("rsa")]
    [InlineData("")]
    [InlineData(" ")]
    public void ClientMapper_ToSignWithKindFixtureOutput_UnknownResponseEnum_FailsLoudValidationFailed(string unknown)
    {
        // A proto response carrying a wire value the client cannot map to the C#
        // enum is a client-side ValidationFailed (400) — strict, NO silent fallback,
        // symmetric with the server inbound request parse + the JSON policy.
        var protoData = new global::D2.Services.Protos.EnumFixtures.V1.SignWithKindFixtureOutput
        {
            Signature = "sig==",
            KeyKind = unknown,
        };

        var result = MapClientOutput(protoData);

        result.Success.Should().BeFalse($"'{unknown}' is not a FixtureKeyKind wire value");
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Data.Should().BeNull("no DTO is produced when the response enum is invalid");
    }

    [Fact]
    public void ClientMapper_ResponseEnum_RoundTripsWithServerOutbound()
    {
        // Server outbound (DTO enum -> proto string via ToWire) ∘ client inbound
        // (proto string -> DTO enum via the response parse) is the identity over the
        // closed enum — proving the server-write and client-read response bridges agree.
        foreach (var kind in new[] { FixtureKeyKind.Rsa, FixtureKeyKind.Aes, FixtureKeyKind.Secret })
        {
            var protoData = new global::D2.Services.Protos.EnumFixtures.V1.SignWithKindFixtureOutput
            {
                Signature = "sig==",
                KeyKind = kind.ToWire(),
            };

            MapClientOutput(protoData).Data!.KeyKind.Should().Be(kind);
        }
    }

    // -------------------------------------------------------------------------
    // Cross-language parity — the shared fixture drives BOTH language suites
    // -------------------------------------------------------------------------

    [Fact]
    public void CrossLang_SharedFixture_EveryMemberWireRoundTripsHere()
    {
        // The TS half (enum-wire-round-trip.test.ts) asserts the SAME wire strings
        // resolve to the SAME members via the const-object — the cross-language
        // contract is that both halves consume the identical fixture file.
        foreach (var en in LoadFixture().Enums)
        {
            en.Members.Should().NotBeEmpty($"enum {en.Name} must carry members");
            foreach (var m in en.Members)
            {
                m.MemberName.Should().NotBeNullOrWhiteSpace();
                m.Wire.Should().NotBeNullOrWhiteSpace();
            }
        }

        // The FixtureKeyKind fixture wires round-trip through the proto mapper here.
        var keyKindFx = LoadFixture().Enums.Single(e => e.Name == "FixtureKeyKind");
        foreach (var m in keyKindFx.Members)
            string.ParseFixtureKeyKindWire(m.Wire).Data.Should().Be(ParseFixtureKeyKind(m.MemberName));
    }

    // -------------------------------------------------------------------------
    // Host + channel helpers
    // -------------------------------------------------------------------------

    private static async Task<IHost> BuildHost(FakeSignWithKindFixtureHandler handler)
    {
        var host = new HostBuilder()
            .ConfigureWebHost(web =>
            {
                web.UseTestServer();
                web.ConfigureServices(services =>
                {
                    services.AddSingleton<ISignWithKindFixtureHandler>(handler);
                    services.AddRouting();
                    services.AddGrpc();
                });
                web.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints =>
                    {
                        endpoints.MapGrpcService<EnumFixturesSignerService>();
                    });
                });
            })
            .Build();

        await host.StartAsync();
        return host;
    }

    private static GrpcChannel CreateChannel(IHost host)
    {
        var httpClient = host.GetTestClient();
        return GrpcChannel.ForAddress(
            httpClient.BaseAddress!,
            new GrpcChannelOptions { HttpClient = httpClient });
    }

    // -------------------------------------------------------------------------
    // Enum member helpers (string member name → typed enum value)
    // -------------------------------------------------------------------------

    // Invokes the generated client response mapper via its declaring static class.
    // The C# 14 extension member `extension(ProtoSignWithKindFixtureOutput data) { ToSignWithKindFixtureOutput() }`
    // lowers to a static method on SignWithKindFixtureClientMappers — callable directly,
    // which avoids importing the Clients namespace (and the ToWire/ParseFixtureKeyKindWire
    // CS0121 ambiguity with the server mapper's identically-named helpers).
    private static D2Result<SignWithKindFixtureOutput> MapClientOutput(
        global::D2.Services.Protos.EnumFixtures.V1.SignWithKindFixtureOutput protoData) =>
        global::D2.Edge.Tests.TypeSpecGrpcEnum.Clients.SignWithKindFixtureClientMappers
            .ToSignWithKindFixtureOutput(protoData);

    private static FixtureKeyKind ParseFixtureKeyKind(string member) => member switch
    {
        "Rsa" => FixtureKeyKind.Rsa,
        "Aes" => FixtureKeyKind.Aes,
        "Secret" => FixtureKeyKind.Secret,
        _ => throw new InvalidDataException($"unknown FixtureKeyKind member '{member}'"),
    };

    private static Level ParseLevel(string member) => member switch
    {
        "Low" => Level.Low,
        "Medium" => Level.Medium,
        "High" => Level.High,
        _ => throw new InvalidDataException($"unknown Level member '{member}'"),
    };

    private static Status ParseStatus(string member) => member switch
    {
        "Active" => Status.Active,
        "Inactive" => Status.Inactive,
        "Pending" => Status.Pending,
        _ => throw new InvalidDataException($"unknown Status member '{member}'"),
    };

    private static bool IsKnownIgnoringCase(string enumName, string wire) =>
        MembersOf(enumName).Any(m => string.Equals(m.Wire, wire, StringComparison.OrdinalIgnoreCase));

    // -------------------------------------------------------------------------
    // Shared-fixture plumbing
    // -------------------------------------------------------------------------

    private static IReadOnlyList<(string MemberName, string Wire)> MembersOf(string enumName)
    {
        var en = LoadFixture().Enums.Single(e => e.Name == enumName);

        return [.. en.Members.Select(m => (m.MemberName, m.Wire))];
    }

    private static IReadOnlyList<string> UnknownValuesOf(string enumName)
    {
        var en = LoadFixture().Enums.SingleOrDefault(e => e.Name == enumName);

        return en?.UnknownValues ?? [];
    }

    private static FixtureFile LoadFixture()
    {
        var path = FindFixturePath();
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureFile>(json, sr_fixtureJson)
            ?? throw new InvalidDataException($"could not parse {path}");
    }

    private static string FindFixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "public",
                "contracts",
                "enum",
                "enum-parity.fixture.json");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(
            "could not locate public/contracts/enum/enum-parity.fixture.json by walking up from "
                + AppContext.BaseDirectory);
    }

    private sealed record FixtureFile(int SchemaVersion, EnumFixture[] Enums);

    private sealed record EnumFixture(
        string Name,
        MemberFixture[] Members,
        string[] UnknownValues);

    private sealed record MemberFixture(string MemberName, string Wire);
}
