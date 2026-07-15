// -----------------------------------------------------------------------
// <copyright file="ScopeSpecLoaderTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.SourceGen;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Scopes.SourceGen;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="ScopeSpecLoader.Load"/>. The loader is
/// responsible only for JSON-shape validation (D2SCP001 — malformed spec); all
/// semantic validation (naming convention, enum values, etc.) is delegated to
/// <see cref="ScopesEmitter"/> and tested separately.
/// </summary>
public sealed class ScopeSpecLoaderTests
{
    // ----------------------------------------------------------------------
    // Happy path
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_HappyPath_ReturnsSpecWithEntries()
    {
        const string json = """
        {
          "scopes": [
            {
              "name": "self.read",
              "description": "Read self",
              "actionSensitivity": "Routine",
              "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] }
            }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec.Should().NotBeNull();
        result.Spec!.Scopes.Should().HaveCount(1);
        var scope = result.Spec.Scopes[0];
        scope.Name.Should().Be("self.read");
        scope.Description.Should().Be("Read self");
        scope.ActionSensitivity.Should().Be("Routine");
        scope.ImpersonationBlocked.Should().BeFalse();
        scope.GrantedTo.Should().NotBeNull();
        scope.GrantedTo!.Should().ContainKey("*");
        scope.GrantedTo["*"].Should().BeEquivalentTo(["*"]);
    }

    [Fact]
    public void Load_AnonScopeWithoutGrantedTo_ReturnsSpecWithNullGrantedTo()
    {
        const string json = """
        {
          "scopes": [
            {
              "name": "anon.public.health",
              "actionSensitivity": "Routine",
              "impersonationBlocked": false
            }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].GrantedTo.Should().BeNull();
        result.Spec.Scopes[0].Description.Should().BeNull();
    }

    [Fact]
    public void Load_MultipleScopes_PreservesOrder()
    {
        const string json = """
        {
          "scopes": [
            { "name": "z.last",   "actionSensitivity": "Routine",
              "impersonationBlocked": false, "grantedTo": { "*": ["*"] } },
            { "name": "a.first",  "actionSensitivity": "Routine",
              "impersonationBlocked": false, "grantedTo": { "*": ["*"] } },
            { "name": "m.middle", "actionSensitivity": "Routine",
              "impersonationBlocked": false, "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Select(s => s.Name)
            .Should().ContainInOrder("z.last", "a.first", "m.middle");
    }

    // ----------------------------------------------------------------------
    // D2SCP001 — malformed JSON / schema-violating
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("{not valid json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("42")]
    [InlineData("\"a string\"")]
    [InlineData("[]")]
    public void Load_NonObjectRoot_EmitsD2SCP001(string json)
    {
        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic.Should().NotBeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_MissingScopesArray_EmitsD2SCP001()
    {
        const string json = """{ "notScopes": [] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Spec.Should().BeNull();
        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("scopes");
    }

    [Fact]
    public void Load_ScopesIsObjectNotArray_EmitsD2SCP001()
    {
        const string json = """{ "scopes": { "x": 1 } }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ScopeEntryIsString_EmitsD2SCP001()
    {
        const string json = """{ "scopes": ["not an object"] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("scopes[0]");
    }

    [Fact]
    public void Load_ScopeMissingName_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [ { "actionSensitivity": "Routine", "impersonationBlocked": false } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("name");
    }

    [Fact]
    public void Load_ScopeNameIsNumber_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [
            { "name": 42, "actionSensitivity": "Routine", "impersonationBlocked": false } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_ScopeMissingActionSensitivity_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [ { "name": "self.read", "impersonationBlocked": false } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("actionSensitivity");
    }

    [Fact]
    public void Load_ScopeMissingImpersonationBlocked_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [ { "name": "self.read", "actionSensitivity": "Routine" } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("impersonationBlocked");
    }

    [Fact]
    public void Load_ImpersonationBlockedIsString_EmitsD2SCP001()
    {
        const string json = """
        { "scopes": [
            { "name": "x.y", "actionSensitivity": "Routine", "impersonationBlocked": "true" } ] }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_GrantedToIsArray_EmitsD2SCP001()
    {
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "actionSensitivity": "Routine", "impersonationBlocked": false,
              "grantedTo": [] }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
        ((string)result.Diagnostic.Args[1]).Should().Contain("grantedTo");
    }

    [Fact]
    public void Load_GrantedToValueIsString_EmitsD2SCP001()
    {
        // grantedTo entry value MUST be an array per the schema.
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "actionSensitivity": "Routine", "impersonationBlocked": false,
              "grantedTo": { "Admin": "Owner" } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    [Fact]
    public void Load_GrantedToRoleEntryIsNumber_EmitsD2SCP001()
    {
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "actionSensitivity": "Routine", "impersonationBlocked": false,
              "grantedTo": { "Admin": [42] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic!.DescriptorId.Should().Be(DiagnosticIds.MalformedSpec);
    }

    // ----------------------------------------------------------------------
    // Adversarial — extra unknown properties
    //
    // The schema's additionalProperties is `false` (editor-time gate), but the
    // loader's responsibility ends at JSON-shape validation. Unknown properties
    // on a scope entry are silently ignored at load-time — schema-level gate
    // catches them in editors / IDEs / a JSON-Schema CLI.
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_UnknownExtraPropertyOnScope_IsIgnoredByLoader()
    {
        const string json = """
        {
          "scopes": [
            { "name": "self.read", "actionSensitivity": "Routine", "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] }, "extraField": "ignored", "anotherExtra": 42 }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Should().HaveCount(1);
        result.Spec.Scopes[0].Name.Should().Be("self.read");
    }

    [Fact]
    public void Load_DescriptionWrongType_IsTreatedAsAbsent()
    {
        // description is optional; a non-string value is silently treated as
        // absent rather than rejected — keeps loader lenient on optional fields.
        const string json = """
        {
          "scopes": [
            { "name": "x.y", "description": 42,
              "actionSensitivity": "Routine", "impersonationBlocked": false,
              "grantedTo": { "*": ["*"] } }
          ]
        }
        """;

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes[0].Description.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Boundary — empty scopes array
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_EmptyScopesArray_ReturnsEmptySpec()
    {
        // Schema's minItems: 1 is editor-time only; loader accepts empty array.
        const string json = """{ "scopes": [] }""";

        var result = ScopeSpecLoader.Load("scopes.spec.json", json);

        result.Diagnostic.Should().BeNull();
        result.Spec!.Scopes.Should().BeEmpty();
    }
}
