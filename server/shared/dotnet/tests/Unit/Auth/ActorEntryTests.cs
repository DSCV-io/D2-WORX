// -----------------------------------------------------------------------
// <copyright file="ActorEntryTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

public sealed class ActorEntryTests
{
    [Fact]
    public void Construction_MinimalServiceEntry_DefaultsAllOptionalFieldsToNull()
    {
        const string subject = "auth-svc";

        var entry = new ActorEntry(ActorKind.Service, subject);

        entry.Kind.Should().Be(ActorKind.Service);
        entry.Subject.Should().Be(subject);
        entry.ClientId.Should().BeNull();
        entry.ImpersonationKind.Should().BeNull();
        entry.SessionId.Should().BeNull();
        entry.OrgId.Should().BeNull();
        entry.OrgName.Should().BeNull();
        entry.OrgType.Should().BeNull();
        entry.OrgRole.Should().BeNull();
        entry.Act.Should().BeNull();
    }

    [Fact]
    public void Construction_ImpersonationEntry_AllFieldsRoundTrip()
    {
        var session_id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var org_id = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var entry = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent-user-id",
            ImpersonationKind: ImpersonationKind.Consent,
            SessionId: session_id,
            OrgId: org_id,
            OrgName: "Customer Support",
            OrgType: OrgType.Support,
            OrgRole: Role.Officer);

        entry.Kind.Should().Be(ActorKind.Impersonation);
        entry.Subject.Should().Be("agent-user-id");
        entry.ImpersonationKind.Should().Be(ImpersonationKind.Consent);
        entry.SessionId.Should().Be(session_id);
        entry.OrgId.Should().Be(org_id);
        entry.OrgName.Should().Be("Customer Support");
        entry.OrgType.Should().Be(OrgType.Support);
        entry.OrgRole.Should().Be(Role.Officer);
    }

    [Fact]
    public void RecordEquality_IdenticalFields_AreEqual()
    {
        var session_id = Guid.NewGuid();

        var a = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            ImpersonationKind: ImpersonationKind.Force,
            SessionId: session_id,
            OrgType: OrgType.Admin,
            OrgRole: Role.Owner);

        var b = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            ImpersonationKind: ImpersonationKind.Force,
            SessionId: session_id,
            OrgType: OrgType.Admin,
            OrgRole: Role.Owner);

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void RecordEquality_DifferentKind_NotEqual()
    {
        var a = new ActorEntry(ActorKind.Service, Subject: "x");
        var b = new ActorEntry(ActorKind.Impersonation, Subject: "x");

        a.Should().NotBe(b);
    }

    [Fact]
    public void RecordEquality_ServiceKindCarriesImpersonationFields_StillRecordsThem()
    {
        // Adversarial: the XML doc says "ImpersonationKind, SessionId, Org*
        // fields are only meaningful when Kind is Impersonation" — but record
        // equality will still consider them. Test that two Service entries
        // differing ONLY in those nominally-meaningless fields are NOT equal,
        // i.e. the fields ARE carried by the record (no contract-level
        // normalization). Documents the actual behaviour: callers must not
        // populate impersonation fields on Service entries unless they want
        // those fields to participate in equality.
        var with_session = new ActorEntry(
            ActorKind.Service,
            Subject: "svc",
            SessionId: Guid.NewGuid());

        var without_session = new ActorEntry(ActorKind.Service, Subject: "svc");

        with_session.Should().NotBe(without_session);
    }

    [Fact]
    public void With_OverrideField_ProducesDistinctRecord()
    {
        var original = new ActorEntry(ActorKind.Service, Subject: "svc-a");

        var modified = original with { Subject = "svc-b" };

        modified.Should().NotBe(original);
        modified.Subject.Should().Be("svc-b");
        original.Subject.Should().Be("svc-a", "with-expression must not mutate original");
    }

    [Fact]
    public void NestedAct_ThreeHopChain_TraversableViaActProperty()
    {
        // Adversarial: build a 3-hop nested actor chain (RFC 8693 §2.1
        // recursion) and verify each hop is reachable + carries its own data.
        var earliest = new ActorEntry(ActorKind.Service, Subject: "originating-svc");
        var middle = new ActorEntry(
            ActorKind.Service,
            Subject: "middle-svc",
            Act: earliest);
        var outermost = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent-id",
            ImpersonationKind: ImpersonationKind.Consent,
            Act: middle);

        outermost.Act.Should().Be(middle);
        outermost.Act!.Act.Should().Be(earliest);
        outermost.Act.Act!.Act.Should().BeNull("earliest entry has no further nesting");

        // Walk the chain explicitly to prove traversal works.
        var current = outermost;
        var depth = 0;
        while (current is not null)
        {
            depth++;
            current = current.Act;
        }

        depth.Should().Be(3);
    }

    [Fact]
    public void RecordEquality_NestedActDifferent_NotEqual()
    {
        // Adversarial: two outermost entries with identical surface fields but
        // DIFFERENT nested Act subtrees must NOT be equal — the chain shape is
        // load-bearing for OriginatingClientId derivation per RFC 8693.
        var nested_a = new ActorEntry(ActorKind.Service, Subject: "deep-a");
        var nested_b = new ActorEntry(ActorKind.Service, Subject: "deep-b");

        var outer_a = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            Act: nested_a);

        var outer_b = new ActorEntry(
            ActorKind.Impersonation,
            Subject: "agent",
            Act: nested_b);

        outer_a.Should().NotBe(outer_b);
    }

    [Fact]
    public void RecordEquality_BothNestedNullVsBothNestedSame_AreEqual()
    {
        // Sanity baseline for the "different Act → different record" test.
        var no_act_a = new ActorEntry(ActorKind.Service, Subject: "svc");
        var no_act_b = new ActorEntry(ActorKind.Service, Subject: "svc");

        no_act_a.Should().Be(no_act_b);

        var nested = new ActorEntry(ActorKind.Service, Subject: "deep");
        var with_act_a = new ActorEntry(ActorKind.Service, Subject: "svc", Act: nested);
        var with_act_b = new ActorEntry(ActorKind.Service, Subject: "svc", Act: nested);

        with_act_a.Should().Be(with_act_b);
    }

    [Fact]
    public void With_OverrideAct_ProducesDifferentRecord()
    {
        var nested = new ActorEntry(ActorKind.Service, Subject: "deep");
        var original = new ActorEntry(ActorKind.Impersonation, Subject: "agent");

        var modified = original with { Act = nested };

        modified.Should().NotBe(original);
        modified.Act.Should().Be(nested);
    }
}
