// -----------------------------------------------------------------------
// <copyright file="ContextEnvelopeJsonTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContext;

using System;
using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using D2.Shared.RequestContext;
using Xunit;

/// <summary>
/// JSON serialization round-trip coverage for <see cref="ContextEnvelope"/>.
/// The envelope is the AMQP / S3 propagation shape — it MUST round-trip
/// losslessly through <see cref="JsonSerializer"/>.
/// </summary>
/// <remarks>
/// **PARSER BUG SURFACED.** The generated <see cref="ContextEnvelope"/>
/// declares <c>Scopes</c> as <c>IReadOnlySet&lt;string&gt;</c> with an
/// <c>init</c>-only setter. <see cref="JsonSerializer"/> CANNOT instantiate
/// <c>IReadOnlySet&lt;string&gt;</c> at deserialize time (it's an interface);
/// every Deserialize call against the envelope throws
/// <see cref="NotSupportedException"/>. The envelope cannot round-trip via
/// System.Text.Json without either a custom converter or changing the
/// generated property type to a concrete <c>HashSet&lt;string&gt;</c>.
/// This is a critical bug for the AMQP/S3 messaging propagation path —
/// captured here as deliberately-failing-to-deserialize tests.
/// </remarks>
public sealed class ContextEnvelopeJsonTests
{
    private static readonly Guid sr_userId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid sr_sessionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid sr_orgId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Serialize_FullEnvelope_ProducesJson()
    {
        // Serialization succeeds; the bug is on the deserialize path.
        var envelope = new ContextEnvelope
        {
            IsAuthenticated = true,
            Audience = ["edge-api"],
            Subject = sr_userId.ToString(),
            UserId = sr_userId,
            OrgId = sr_orgId,
            OrgType = OrgType.Customer,
            OrgRole = Role.Owner,
        };

        var json = JsonSerializer.Serialize(envelope);

        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("Subject");
        json.Should().Contain("Audience");
    }

    [Fact]
    public void Deserialize_EnvelopeWithScopes_RoundTripsCleanly()
    {
        // The envelope is the AMQP / S3 cross-transport propagation shape,
        // which means it MUST round-trip through System.Text.Json on the
        // consume side. The generator emits the Scopes property as the
        // concrete HashSet<string> (not IReadOnlySet<string>) so STJ can
        // construct it; consumers that read via the IReadOnlySet contract
        // still work because HashSet IS-A IReadOnlySet.
        var envelope = new ContextEnvelope
        {
            Scopes = new HashSet<string>(StringComparer.Ordinal) { "self.read", "self.write" },
        };

        var json = JsonSerializer.Serialize(envelope);
        var roundTripped = JsonSerializer.Deserialize<ContextEnvelope>(json);

        roundTripped.Should().NotBeNull();
        roundTripped.Scopes.Should().BeEquivalentTo(["self.read", "self.write"]);
    }

    [Fact]
    public void Deserialize_EnvelopeWithoutScopesField_Succeeds()
    {
        // Sanity: Scopes is the ONLY property triggering the deserialize bug.
        // To prove the rest deserializes fine, hand-roll JSON that omits Scopes.
        // Note: System.Text.Json default serializes enums as ints (e.g.
        // OrgType.Customer == 2). Use the int form here.
        const string json = """
        {
            "IsAuthenticated": true,
            "Subject": "alice",
            "OrgType": 2
        }
        """;

        var envelope = JsonSerializer.Deserialize<ContextEnvelope>(json);

        envelope.Should().NotBeNull();
        envelope.IsAuthenticated.Should().BeTrue();
        envelope.Subject.Should().Be("alice");
        envelope.OrgType.Should().Be(OrgType.Customer);
    }

    [Fact]
    public void Deserialize_EmptyObjectWithoutScopes_AllPropertiesNull()
    {
        // Empty JSON (no Scopes key) → default ContextEnvelope (all nulls).
        const string json = "{}";

        var envelope = JsonSerializer.Deserialize<ContextEnvelope>(json);

        envelope.Should().NotBeNull();
        envelope.IsAuthenticated.Should().BeNull();
        envelope.Subject.Should().BeNull();
        envelope.UserId.Should().BeNull();
        envelope.OrgId.Should().BeNull();
        envelope.OrgType.Should().BeNull();
    }

    [Fact]
    public void Deserialize_UnknownProperties_Ignored()
    {
        // Adversarial: future / extra fields on the wire must NOT throw.
        // System.Text.Json defaults to ignoring unknown properties.
        // Property names use PascalCase (default System.Text.Json policy).
        const string json = """
        {
            "IsAuthenticated": true,
            "Subject": "alice",
            "futureField": "from-tomorrow",
            "anotherUnknown": { "nested": [1,2,3] }
        }
        """;

        var act = () => JsonSerializer.Deserialize<ContextEnvelope>(json);

        var envelope = act.Should().NotThrow().Subject;
        envelope!.IsAuthenticated.Should().BeTrue();
        envelope.Subject.Should().Be("alice");
    }

    [Fact]
    public void Serialize_PreservesActorChain_OnSerializeSide()
    {
        // Even though deserialize is broken (Scopes bug), serialize correctly
        // emits the ActorChain entries. Validates the serialize half of the
        // round-trip.
        var actor1 = new ActorEntry(ActorKind.Service, "edge-svc", ClientId: "edge-app");
        var actor2 = new ActorEntry(
            ActorKind.Impersonation,
            sr_userId.ToString(),
            ImpersonationKind: D2.Shared.Auth.Abstractions.ImpersonationKind.Consent,
            SessionId: sr_sessionId,
            OrgId: sr_orgId,
            OrgType: OrgType.Support,
            OrgRole: Role.Agent);

        var envelope = new ContextEnvelope
        {
            ActorChain = [actor1, actor2],
        };

        var json = JsonSerializer.Serialize(envelope);

        // Default System.Text.Json emits enums as their underlying integer.
        // ImpersonationKind.Consent == 0; OrgType.Support == 1; ActorKind.Service == 0.
        // String emission would require a JsonStringEnumConverter on the
        // serializer options. Verify the structural content is present.
        json.Should().Contain("edge-svc");
        json.Should().Contain("edge-app");
        json.Should().Contain(sr_userId.ToString());
    }
}
