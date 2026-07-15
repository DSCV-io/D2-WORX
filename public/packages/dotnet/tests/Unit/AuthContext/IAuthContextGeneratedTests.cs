// -----------------------------------------------------------------------
// <copyright file="IAuthContextGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.AuthContext;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Reflection smoke proving the codegen-emitted <see cref="IAuthContext"/>
/// interface carries every property declared in the spec, with the right
/// CLR type. Drift gate — adding a property to the spec without rebuilding,
/// or changing a property's type without re-running the generator, breaks
/// here loudly.
/// </summary>
public sealed class IAuthContextGeneratedTests
{
    [Fact]
    public void Interface_HasEveryPropertyDeclaredInSpec()
    {
        var specPath = TestPaths.AuthContextSpec();
        File.Exists(specPath).Should().BeTrue("spec must be present at " + specPath);

        var spec = JsonDocument.Parse(File.ReadAllText(specPath));

        var specPropertyNames = new List<string>();
        foreach (var section in spec.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
                specPropertyNames.Add(property.GetProperty("name").GetString()!);
        }

        specPropertyNames.Should().NotBeEmpty();

        var declared = typeof(IAuthContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        var missing = specPropertyNames.Where(n => !declared.Contains(n)).ToList();
        missing.Should().BeEmpty(
            "every spec property must appear on IAuthContext; missing: "
            + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("IsAuthenticated", typeof(bool?))]
    [InlineData("Audience", typeof(IReadOnlyList<string>))]
    [InlineData("SessionId", typeof(Guid?))]
    [InlineData("TokenIssuedAt", typeof(DateTimeOffset?))]
    [InlineData("TokenExpiresAt", typeof(DateTimeOffset?))]
    [InlineData("ActorChain", typeof(IReadOnlyList<ActorEntry>))]
    [InlineData("Subject", typeof(string))]
    [InlineData("UserId", typeof(Guid?))]
    [InlineData("Username", typeof(string))]
    [InlineData("RequestedByClientId", typeof(string))]
    [InlineData("ImmediateCallerClientId", typeof(string))]
    [InlineData("OriginatingClientId", typeof(string))]
    [InlineData("IsServiceIdentity", typeof(bool?))]
    [InlineData("OrgId", typeof(Guid?))]
    [InlineData("OrgName", typeof(string))]
    [InlineData("OrgType", typeof(OrgType?))]
    [InlineData("OrgRole", typeof(Role?))]
    [InlineData("IsImpersonating", typeof(bool?))]
    [InlineData("ImpersonationKind", typeof(ImpersonationKind?))]
    [InlineData("ImpersonatedBy", typeof(Guid?))]
    [InlineData("ImpersonationSessionId", typeof(Guid?))]
    [InlineData("ImpersonatorOrgId", typeof(Guid?))]
    [InlineData("ImpersonatorOrgName", typeof(string))]
    [InlineData("ImpersonatorOrgType", typeof(OrgType?))]
    [InlineData("ImpersonatorOrgRole", typeof(Role?))]
    [InlineData("Scopes", typeof(IReadOnlySet<string>))]
    [InlineData("AuthMethod", typeof(string))]
    [InlineData("LastStepUpAt", typeof(DateTimeOffset?))]
    public void Property_HasExpectedClrType(string propertyName, Type expectedType)
    {
        var property = typeof(IAuthContext).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);

        property.Should().NotBeNull(propertyName + " must be declared on IAuthContext");
        property.PropertyType.Should().Be(expectedType);
    }

    [Fact]
    public void Properties_AreReadOnly_GettersOnly()
    {
        // Adversarial: IAuthContext is the read-only contract surface. Adding
        // a setter on the interface would let any handler mutate the live
        // request context — a critical encapsulation breach.
        foreach (var property in typeof(IAuthContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            property.CanRead.Should().BeTrue(property.Name + " must have a getter");
            property.CanWrite.Should().BeFalse(
                property.Name + " must NOT have a setter on the interface");
        }
    }
}
