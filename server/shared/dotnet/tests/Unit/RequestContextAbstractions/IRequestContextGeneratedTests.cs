// -----------------------------------------------------------------------
// <copyright file="IRequestContextGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.RequestContextAbstractions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using D2.Shared.AuthContext.Abstractions;
using D2.Shared.RequestContext.Abstractions;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Reflection smoke proving the codegen-emitted <see cref="IRequestContext"/>
/// interface carries every NEW property in the spec (transport / network /
/// fingerprint / WhoIs sections) AND inherits every <see cref="IAuthContext"/>
/// property via <c>extends</c>.
/// </summary>
public sealed class IRequestContextGeneratedTests
{
    [Fact]
    public void Interface_HasEveryPropertyDeclaredInRequestContextSpec()
    {
        var spec_path = TestPaths.RequestContextSpec();
        File.Exists(spec_path).Should().BeTrue("spec must be present at " + spec_path);

        var spec = JsonDocument.Parse(File.ReadAllText(spec_path));

        var spec_property_names = new List<string>();
        foreach (var section in spec.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
                spec_property_names.Add(property.GetProperty("name").GetString()!);
        }

        spec_property_names.Should().NotBeEmpty();

        // GetProperties (no DeclaredOnly) returns inherited too — but we want
        // to assert each NEW spec property is reachable on IRequestContext.
        // Use BindingFlags including FlattenHierarchy.
        var reachable_via_request = typeof(IRequestContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy)
            .Select(p => p.Name)
            .Concat(typeof(IRequestContext).GetInterfaces()
                .SelectMany(i => i.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(p => p.Name))
            .ToHashSet();

        var missing = spec_property_names.Where(n => !reachable_via_request.Contains(n)).ToList();
        missing.Should().BeEmpty(
            "every IRequestContext spec property must appear on the generated interface; "
            + "missing: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("TraceId", typeof(string))]
    [InlineData("RequestId", typeof(string))]
    [InlineData("RequestPath", typeof(string))]
    [InlineData("IsSyntheticEnvelope", typeof(bool?))]
    [InlineData("ClientIp", typeof(string))]
    [InlineData("SessionFingerprint", typeof(string))]
    [InlineData("CurrentFingerprint", typeof(string))]
    [InlineData("FingerprintMatchScore", typeof(int?))]
    [InlineData("WhoIsHashId", typeof(string))]
    [InlineData("AdminLocationHashId", typeof(string))]
    [InlineData("City", typeof(string))]
    [InlineData("Region", typeof(string))]
    [InlineData("SubdivisionCode", typeof(string))]
    [InlineData("CountryCode", typeof(string))]
    [InlineData("PostalCode", typeof(string))]
    [InlineData("Latitude", typeof(double?))]
    [InlineData("Longitude", typeof(double?))]
    [InlineData("Geohash", typeof(string))]
    [InlineData("IsVpn", typeof(bool?))]
    [InlineData("IsProxy", typeof(bool?))]
    [InlineData("IsTor", typeof(bool?))]
    [InlineData("IsHosting", typeof(bool?))]
    [InlineData("Asn", typeof(int?))]
    [InlineData("AsnName", typeof(string))]
    [InlineData("AsnType", typeof(string))]
    public void NewProperty_HasExpectedClrType(string propertyName, Type expectedType)
    {
        var property = typeof(IRequestContext).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);

        property.Should().NotBeNull(
            propertyName + " must be declared on IRequestContext");
        property.PropertyType.Should().Be(expectedType);
    }

    [Theory]
    [InlineData("IsAuthenticated")]
    [InlineData("Subject")]
    [InlineData("UserId")]
    [InlineData("Scopes")]
    [InlineData("OrgType")]
    [InlineData("ImpersonationKind")]
    [InlineData("ActorChain")]
    [InlineData("Audience")]
    public void InheritedAuthProperty_AccessibleViaIRequestContext(string propertyName)
    {
        // Adversarial: assert IAuthContext properties are reachable through
        // IRequestContext (i.e. the codegen wired the `extends` correctly).
        // A consumer holding an IRequestContext should not need to cast to
        // IAuthContext to read auth fields.
        var auth_property = typeof(IAuthContext).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);

        auth_property.Should().NotBeNull(propertyName + " must exist on IAuthContext");

        // The inherited member is reachable via the interface map.
        var all_reachable = new HashSet<string>(
            typeof(IRequestContext).GetProperties().Select(p => p.Name));
        foreach (var iface in typeof(IRequestContext).GetInterfaces())
        {
            foreach (var p in iface.GetProperties())
                all_reachable.Add(p.Name);
        }

        all_reachable.Should().Contain(propertyName);
    }

    [Fact]
    public void Properties_AreReadOnly_GettersOnly()
    {
        // Adversarial: same encapsulation discipline as IAuthContext.
        var declared = typeof(IRequestContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var property in declared)
        {
            property.CanRead.Should().BeTrue(property.Name + " must have a getter");
            property.CanWrite.Should().BeFalse(
                property.Name + " must NOT have a setter on the interface");
        }
    }
}
