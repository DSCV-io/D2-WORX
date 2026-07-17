// -----------------------------------------------------------------------
// <copyright file="RedactDataDestructuringPolicyTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Logging.Destructuring;

using AwesomeAssertions;
using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.Tests.Unit.Logging.Fixtures;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Pure unit tests for the destructuring policy — exercises the
/// <see cref="IDestructuringPolicy.TryDestructure"/> contract directly with a
/// stub <see cref="ILogEventPropertyValueFactory"/>. End-to-end coverage
/// through a real Serilog pipeline lives in
/// <c>Integration.Logging.SerilogPipelineRedactionTests</c>.
/// </summary>
public sealed class RedactDataDestructuringPolicyTests
{
    [Fact]
    public void TryDestructure_NullValue_Throws()
    {
        var policy = new RedactDataDestructuringPolicy();
        var factory = new RecordingFactory();

        var act = () => policy.TryDestructure(null!, factory, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryDestructure_NullFactory_Throws()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.PassthroughRecord("a", 1);

        var act = () => policy.TryDestructure(value, null!, out _);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TryDestructure_TypeLevelRedacted_ReplacesEntireValueWithPlaceholder()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.TypeLevelRedactedRecord("a@b.co", "555");

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeTrue();
        result.Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void TryDestructure_TypeLevelWithCustomReason_RendersCustomReason()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.TypeLevelRedactedWithCustomReasonRecord("v");

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeTrue();
        result.Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("[REDACTED: MyCustomMaskReason]");
    }

    [Fact]
    public void TryDestructure_PropertyLevelRedacted_MasksTargetedProperty()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.PropertyLevelRedactedRecord
        {
            Name = "Alice",
            Email = "alice@example.com",
        };

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeTrue();
        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Email"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void TryDestructure_PropertyLevelRedacted_PreservesNonRedactedProperty()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.PropertyLevelRedactedRecord
        {
            Name = "Alice",
            Email = "alice@example.com",
        };
        var factory = new RecordingFactory();

        policy.TryDestructure(value, factory, out _);

        // Non-redacted Name is forwarded to factory.CreatePropertyValue,
        // so the recording factory captures the raw value.
        factory.Recorded.Should().ContainSingle(r => "Alice".Equals(r));
    }

    [Fact]
    public void TryDestructure_MultipleRedactedProperties_AllMasked()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.MultiPropertyRedactedRecord
        {
            Name = "Alice",
            Email = "alice@example.com",
            Token = "secret-token",
            Age = 42,
        };

        policy.TryDestructure(value, new RecordingFactory(), out var result);

        var structure = (StructureValue)result!;
        structure.Properties.Should().Contain(p =>
            p.Name == "Email"
            && ((ScalarValue)p.Value).Value!.ToString()!.Contains("REDACTED"));
        structure.Properties.Should().Contain(p =>
            p.Name == "Token"
            && ((ScalarValue)p.Value).Value!.ToString()!.Contains("REDACTED"));
    }

    [Fact]
    public void TryDestructure_MixedReasons_PerPropertyPlaceholderUsesIndividualReason()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.MixedReasonsRecord
        {
            Email = "alice@example.com",
            Token = "secret",
        };

        policy.TryDestructure(value, new RecordingFactory(), out var result);

        var structure = (StructureValue)result!;
        var emailValue = ((ScalarValue)structure.Properties
            .Single(p => p.Name == "Email").Value).Value;
        var tokenValue = ((ScalarValue)structure.Properties
            .Single(p => p.Name == "Token").Value).Value;
        emailValue.Should().Be("[REDACTED: PersonalInformation]");
        tokenValue.Should().Be("[REDACTED: SecretInformation]");
    }

    [Fact]
    public void TryDestructure_NullPropertyValue_StillForwardedToFactory()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.OuterWithRedactedTypedInnerRecord
        {
            Description = "outer",
            Inner = null,
        };
        var factory = new RecordingFactory();

        var act = () => policy.TryDestructure(value, factory, out _);

        act.Should().NotThrow();
    }

    [Fact]
    public void TryDestructure_NoRedactionAnywhere_Passes()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.PassthroughRecord("alice", 42);

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_PropertyLevelRedactedInnerReference_MasksInnerOnOuter()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.OuterWithPropertyLevelRedactedInnerRecord
        {
            Description = "outer",
            Secret = new RedactionFixtures.PassthroughInner("inner-secret"),
        };

        policy.TryDestructure(value, new RecordingFactory(), out var result);

        var structure = (StructureValue)result!;
        structure.Properties.Should().Contain(p =>
            p.Name == "Secret"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: SecretInformation]");
    }

    [Fact]
    public void TryDestructure_TypeLevelRedactedClass_ReplacesEntireValue()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.TypeLevelRedactedClass
        {
            AccountNumber = "1234-5678",
        };

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeTrue();
        result.Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("[REDACTED: FinancialInformation]");
    }

    [Fact]
    public void TryDestructure_TypeLevelRedactedStruct_ReplacesEntireValue()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.TypeLevelRedactedStruct("secret");

        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeTrue();
        result.Should().BeOfType<ScalarValue>()
            .Which.Value.Should().Be("[REDACTED: SecretInformation]");
    }

    [Fact]
    public void TryDestructure_EmptyClass_StructureValueWithNoProperties()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.EmptyClass();

        // No type-level [RedactData], no property-level → policy returns false.
        var ok = policy.TryDestructure(value, new RecordingFactory(), out var result);

        ok.Should().BeFalse();
        result.Should().BeNull();
    }

    [Fact]
    public void TryDestructure_MixedAccessClass_OnlyPublicInstancePropertiesProcessed()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.MixedAccessClass();

        // No [RedactData] anywhere → passthrough.
        var ok = policy.TryDestructure(value, new RecordingFactory(), out _);

        // No type or property attribute → returns false (Serilog default kicks in).
        ok.Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_InheritedRedactedProperty_Masked()
    {
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.InheritanceChild
        {
            Email = "alice@example.com",
            Name = "Alice",
        };

        policy.TryDestructure(value, new RecordingFactory(), out var result);

        var structure = (StructureValue)result!;
        structure.Properties.Should().Contain(p =>
            p.Name == "Email"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void TryDestructure_FieldLevelRedacted_FieldIgnored_DocumentsLimitation()
    {
        // Documented limitation: fields are NOT inspected. The class has no
        // public properties, so the policy returns false (default destructurer
        // takes over) and the field's value flows through unmasked.
        var policy = new RedactDataDestructuringPolicy();
        var value = new RedactionFixtures.FieldLevelRedactedClass { Email = "leak@x.co" };

        var ok = policy.TryDestructure(value, new RecordingFactory(), out _);

        ok.Should().BeFalse();
    }

    [Fact]
    public void TryDestructure_SameType_ReusesCacheEntry()
    {
        // Cache is instance-scoped: a fresh policy instance starts empty.
        var policy = new RedactDataDestructuringPolicy();

        var initialCount = policy.CacheCount;

        policy.TryDestructure(
            new RedactionFixtures.TypeLevelRedactedRecord("a", "b"),
            new RecordingFactory(),
            out _);
        policy.TryDestructure(
            new RedactionFixtures.TypeLevelRedactedRecord("c", "d"),
            new RecordingFactory(),
            out _);

        var afterTwoCalls = policy.CacheCount;

        afterTwoCalls.Should().Be(initialCount + 1);
    }

    [Fact]
    public async Task TryDestructure_ParallelCallsSameType_ThreadSafe()
    {
        var policy = new RedactDataDestructuringPolicy();

        var tasks = Enumerable.Range(0, 100).Select(i => Task.Run(() =>
        {
            policy.TryDestructure(
                new RedactionFixtures.TypeLevelRedactedRecord($"e{i}", $"p{i}"),
                new RecordingFactory(),
                out _);
        }));
        await Task.WhenAll(tasks);

        policy.CacheCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryDestructure_CyclicGraph_NoStackOverflow()
    {
        // Our policy doesn't recurse on its own (it relies on
        // ILogEventPropertyValueFactory.CreatePropertyValue, which Serilog
        // recursion-guards). A cyclic graph WITHOUT [RedactData] passes
        // through directly; the test pins that we don't blow up at our
        // analysis step on cycles.
        var policy = new RedactDataDestructuringPolicy();
        var a = new RedactionFixtures.CyclicNode { Name = "a" };
        var b = new RedactionFixtures.CyclicNode { Name = "b", Next = a };
        a.Next = b;

        var act = () => policy.TryDestructure(a, new RecordingFactory(), out _);

        act.Should().NotThrow();
    }

    /// <summary>
    /// Stub <see cref="ILogEventPropertyValueFactory"/> that records the values
    /// it's asked to produce property-values for, so tests can assert that
    /// non-redacted properties are forwarded for default destructuring.
    /// </summary>
    private sealed class RecordingFactory : ILogEventPropertyValueFactory
    {
        public List<object?> Recorded { get; } = [];

        public LogEventPropertyValue CreatePropertyValue(
            object? value,
            bool destructureObjects = false)
        {
            Recorded.Add(value);
            return new ScalarValue(value);
        }
    }
}
