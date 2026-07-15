// -----------------------------------------------------------------------
// <copyright file="TKMessageTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n;

using System.Collections.Generic;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using Xunit;

public sealed class TKMessageTests
{
    // ----------------------------------------------------------------------
    // SrcGen-emitted constants
    // ----------------------------------------------------------------------

    [Fact]
    public void GeneratedConstants_HaveCorrectKey()
    {
        TK.Common.Errors.NOT_FOUND.Key.Should().Be("common_errors_NOT_FOUND");
        TK.Common.Errors.UNKNOWN.Key.Should().Be("common_errors_UNKNOWN");
        TK.Auth.Errors.UNAUTHORIZED.Key.Should().Be("auth_errors_UNAUTHORIZED");
        TK.Geo.Validation.IP_REQUIRED.Key.Should().Be("geo_validation_ip_required");
    }

    [Fact]
    public void GeneratedConstants_HaveNoParameters()
    {
        // Adversarial: every SrcGen-emitted constant ships with null Parameters â€”
        // params are bound only via With() at the call site.
        TK.Common.Errors.NOT_FOUND.Parameters.Should().BeNull();
    }

    [Fact]
    public void GeneratedConstants_AreSameInstanceAcrossAccesses()
    {
        // Static readonly â€” referencing TK.X.Y.Z twice returns the same object.
        var first = TK.Common.Errors.NOT_FOUND;
        var second = TK.Common.Errors.NOT_FOUND;

        ReferenceEquals(first, second).Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // With(name, value) â€” bind a single parameter
    // ----------------------------------------------------------------------

    [Fact]
    public void With_NameValue_ReturnsNewInstanceWithBoundParam()
    {
        var bound = TK.Common.Errors.NOT_FOUND.With("entity", "user");

        bound.Should().NotBeSameAs(TK.Common.Errors.NOT_FOUND);
        bound.Key.Should().Be("common_errors_NOT_FOUND");
        bound.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["entity"] = "user" });
    }

    [Fact]
    public void With_DoesNotMutateReceiver()
    {
        // Adversarial: With must NOT mutate the source TKMessage.
        // Receiver immutability is the entire point of the static-readonly TK pattern â€”
        // if With mutated, every call site sharing the constant would see foreign params.
        var original = TK.Common.Errors.NOT_FOUND;

        _ = original.With("a", "1");

        original.Parameters.Should().BeNull();
    }

    [Fact]
    public void With_TwiceChained_AccumulatesBothParams()
    {
        var bound = TK.Common.Errors.NOT_FOUND
            .With("a", "1")
            .With("b", "2");

        bound.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" });
    }

    [Fact]
    public void With_SameNameTwice_LastValueWins()
    {
        var bound = TK.Common.Errors.NOT_FOUND
            .With("entity", "user")
            .With("entity", "org");

        bound.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["entity"] = "org" });
    }

    [Fact]
    public void With_NullName_Throws()
    {
        var act = () => TK.Common.Errors.NOT_FOUND.With(name: null!, value: "x");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void With_EmptyOrWhitespaceName_Throws()
    {
        var actEmpty = () => TK.Common.Errors.NOT_FOUND.With(string.Empty, "x");
        actEmpty.Should().Throw<ArgumentException>();

        var actWs = () => TK.Common.Errors.NOT_FOUND.With("   ", "x");
        actWs.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void With_NullValue_Throws()
    {
        var act = () => TK.Common.Errors.NOT_FOUND.With("name", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void With_EmptyStringValue_IsAllowed()
    {
        // Boundary: empty-string value is a legitimate substitution (e.g. clearing
        // a placeholder). Whitespace-only / empty are NOT the same as null at this layer.
        var bound = TK.Common.Errors.NOT_FOUND.With("name", string.Empty);

        bound.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["name"] = string.Empty });
    }

    // ----------------------------------------------------------------------
    // With(IReadOnlyDictionary<string, string>) â€” replace whole dict
    // ----------------------------------------------------------------------

    [Fact]
    public void WithDictionary_ReplacesParameters()
    {
        // Adversarial: With(dict) REPLACES; it doesn't merge with prior params.
        var firstBound = TK.Common.Errors.NOT_FOUND.With("a", "1");

        var replaced = firstBound.With(
            new Dictionary<string, string> { ["b"] = "2" });

        replaced.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["b"] = "2" });
    }

    [Fact]
    public void WithDictionary_NullDict_Throws()
    {
        var act = () => TK.Common.Errors.NOT_FOUND.With(parameters: null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void WithDictionary_EmptyDict_AllowedAndStored()
    {
        var bound = TK.Common.Errors.NOT_FOUND.With(new Dictionary<string, string>());

        bound.Parameters.Should().NotBeNull();
        bound.Parameters!.Count.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // Equality â€” record-style with order-independent params
    // ----------------------------------------------------------------------

    [Fact]
    public void Equality_SameKeyNoParams_AreEqual()
    {
        var a = TK.Common.Errors.NOT_FOUND;
        var b = TK.Common.Errors.NOT_FOUND;

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_DifferentKey_AreNotEqual()
    {
        TK.Common.Errors.NOT_FOUND.Should().NotBe(TK.Common.Errors.FORBIDDEN);
    }

    [Fact]
    public void Equality_SameKeyAndParams_AreEqual()
    {
        var a = TK.Common.Errors.NOT_FOUND.With("entity", "user");
        var b = TK.Common.Errors.NOT_FOUND.With("entity", "user");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_SameKeyDifferentParamValue_AreNotEqual()
    {
        var a = TK.Common.Errors.NOT_FOUND.With("entity", "user");
        var b = TK.Common.Errors.NOT_FOUND.With("entity", "org");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_SameKeyDifferentParamKey_AreNotEqual()
    {
        var a = TK.Common.Errors.NOT_FOUND.With("a", "1");
        var b = TK.Common.Errors.NOT_FOUND.With("b", "1");

        a.Should().NotBe(b);
    }

    [Fact]
    public void Equality_OrderIndependentParameterDictionaries_AreEqual()
    {
        // Adversarial: params are a dictionary, NOT an ordered sequence.
        // Two TKMessages built up in different param-add order must be equal.
        var a = TK.Common.Errors.NOT_FOUND.With("a", "1").With("b", "2");
        var b = TK.Common.Errors.NOT_FOUND.With("b", "2").With("a", "1");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equality_OneNullOneEmptyParams_AreNotEqual()
    {
        // Adversarial: a TKMessage with null Parameters is structurally distinct
        // from one with an empty dictionary. The wire format reflects this:
        // null â†’ no "params" key; empty â†’ no "params" key on Write (per converter).
        // But Equals doesn't know about Write semantics; it compares fields.
        var nullParams = TK.Common.Errors.NOT_FOUND;
        var emptyParams = TK.Common.Errors.NOT_FOUND.With(new Dictionary<string, string>());

        nullParams.Should().NotBe(emptyParams);
    }

    [Fact]
    public void Equality_AgainstNull_ReturnsFalse()
    {
        TK.Common.Errors.NOT_FOUND.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void Equality_ReferenceEquality_FastPath()
    {
        var a = TK.Common.Errors.NOT_FOUND;

        // Same reference returns true regardless of field comparison.
        a.Equals(a).Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // JSON serialization â€” wire format
    // ----------------------------------------------------------------------

    [Fact]
    public void JsonSerialize_NoParams_OmitsParamsProperty()
    {
        var json = JsonSerializer.Serialize(TK.Common.Errors.NOT_FOUND);

        json.Should().Be(@"{""key"":""common_errors_NOT_FOUND""}");
    }

    [Fact]
    public void JsonSerialize_WithParams_IncludesParamsProperty()
    {
        var msg = TK.Common.Errors.NOT_FOUND.With("entity", "user");

        var json = JsonSerializer.Serialize(msg);

        json.Should().Be(
            @"{""key"":""common_errors_NOT_FOUND"",""params"":{""entity"":""user""}}");
    }

    [Fact]
    public void JsonSerialize_EmptyParamsDict_OmitsParamsProperty()
    {
        // Boundary: Parameters with Count==0 is treated as no-params on the wire
        // (consistent with the no-params constant case). Avoids gratuitous "params":{}.
        var msg = TK.Common.Errors.NOT_FOUND.With(new Dictionary<string, string>());

        var json = JsonSerializer.Serialize(msg);

        json.Should().Be(@"{""key"":""common_errors_NOT_FOUND""}");
    }

    [Fact]
    public void JsonDeserialize_NoParams_ProducesEquivalentMessage()
    {
        var msg = JsonSerializer.Deserialize<TKMessage>(
            @"{""key"":""common_errors_NOT_FOUND""}");

        msg.Should().NotBeNull();
        msg.Key.Should().Be("common_errors_NOT_FOUND");
        msg.Parameters.Should().BeNull();
    }

    [Fact]
    public void JsonDeserialize_WithParams_ProducesEquivalentMessage()
    {
        var msg = JsonSerializer.Deserialize<TKMessage>(
            @"{""key"":""auth_errors_PASSWORD_WEAK"",""params"":{""minLength"":""12""}}");

        msg.Should().NotBeNull();
        msg.Key.Should().Be("auth_errors_PASSWORD_WEAK");
        msg.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["minLength"] = "12" });
    }

    [Fact]
    public void JsonRoundTrip_NoParams_PreservesEquality()
    {
        var original = TK.Common.Errors.NOT_FOUND;

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TKMessage>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void JsonRoundTrip_WithParams_PreservesEquality()
    {
        var original = TK.Common.Errors.NOT_FOUND.With("a", "1").With("b", "2");

        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<TKMessage>(json);

        deserialized.Should().Be(original);
    }

    [Fact]
    public void JsonDeserialize_PropertyOrderReversed_StillSucceeds()
    {
        var msg = JsonSerializer.Deserialize<TKMessage>(
            @"{""params"":{""x"":""y""},""key"":""auth_errors_INVALID_ROLE""}");

        msg.Should().NotBeNull();
        msg.Key.Should().Be("auth_errors_INVALID_ROLE");
        msg.Parameters.Should().BeEquivalentTo(
            new Dictionary<string, string> { ["x"] = "y" });
    }

    [Fact]
    public void JsonDeserialize_UnknownProperty_IsIgnored()
    {
        // Adversarial: future wire-format additions must not break old parsers.
        // The converter ignores properties it doesn't recognize.
        var msg = JsonSerializer.Deserialize<TKMessage>(
            @"{""key"":""common_errors_NOT_FOUND"",""severity"":""error""}");

        msg.Should().NotBeNull();
        msg.Key.Should().Be("common_errors_NOT_FOUND");
    }

    [Fact]
    public void JsonDeserialize_MissingKey_Throws()
    {
        // Adversarial: every TKMessage MUST carry a key. JSON without one is invalid.
        var act = () => JsonSerializer.Deserialize<TKMessage>(@"{""params"":{""a"":""1""}}");

        act.Should().Throw<JsonException>()
            .WithMessage("*key*");
    }

    [Fact]
    public void JsonDeserialize_NullJson_ReturnsNull()
    {
        var msg = JsonSerializer.Deserialize<TKMessage>("null");

        msg.Should().BeNull();
    }

    [Fact]
    public void JsonDeserialize_NotAnObject_Throws()
    {
        var actArray = () => JsonSerializer.Deserialize<TKMessage>("[]");
        actArray.Should().Throw<JsonException>();

        var actString = () => JsonSerializer.Deserialize<TKMessage>(@"""string""");
        actString.Should().Throw<JsonException>();

        var actNumber = () => JsonSerializer.Deserialize<TKMessage>("42");
        actNumber.Should().Throw<JsonException>();
    }

    [Fact]
    public void JsonDeserialize_NullParamsProperty_TreatedAsNoParams()
    {
        // Boundary: explicit "params": null is allowed and means "no params".
        var msg = JsonSerializer.Deserialize<TKMessage>(
            @"{""key"":""common_errors_NOT_FOUND"",""params"":null}");

        msg.Should().NotBeNull();
        msg.Parameters.Should().BeNull();
    }

    [Fact]
    public void JsonSerialize_InsideArray_PreservesShape()
    {
        // Smoke test for the actual D2Result.Messages wire format, which is
        // an array of TKMessage objects.
        TKMessage[] messages =
        [
            TK.Common.Errors.NOT_FOUND,
            TK.Common.Errors.UNAUTHORIZED.With("scope", "admin"),
        ];

        var json = JsonSerializer.Serialize(messages);

        json.Should().Be(
            @"[{""key"":""common_errors_NOT_FOUND""}," +
            @"{""key"":""common_errors_UNAUTHORIZED"",""params"":{""scope"":""admin""}}]");
    }
}
