// -----------------------------------------------------------------------
// <copyright file="IRequestContextGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.RequestContextAbstractions;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AwesomeAssertions;
using DcsvIo.D2.AuthContext.Abstractions;
using DcsvIo.D2.Context.Abstractions;
using DcsvIo.D2.Tests.Unit.Auth;
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
        var specPath = TestPaths.RequestContextSpec();
        File.Exists(specPath).Should().BeTrue("spec must be present at " + specPath);

        var spec = JsonDocument.Parse(File.ReadAllText(specPath));

        var specPropertyNames = new List<string>();
        foreach (var section in spec.RootElement.GetProperty("sections").EnumerateArray())
        {
            foreach (var property in section.GetProperty("properties").EnumerateArray())
                specPropertyNames.Add(property.GetProperty("name").GetString()!);
        }

        specPropertyNames.Should().NotBeEmpty();

        // GetProperties (no DeclaredOnly) returns inherited too — but we want
        // to assert each NEW spec property is reachable on IRequestContext.
        // Use BindingFlags including FlattenHierarchy.
        const BindingFlags public_instance_flat =
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;
        var reachableViaRequest = typeof(IRequestContext)
            .GetProperties(public_instance_flat)
            .Select(p => p.Name)
            .Concat(typeof(IRequestContext).GetInterfaces()
                .SelectMany(i => i.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                .Select(p => p.Name))
            .ToHashSet();

        var missing = specPropertyNames.Where(n => !reachableViaRequest.Contains(n)).ToList();
        missing.Should().BeEmpty(
            "every IRequestContext spec property must appear on the generated interface; "
            + "missing: " + string.Join(", ", missing));
    }

    [Theory]
    [InlineData("TraceId", typeof(string))]
    [InlineData("RequestId", typeof(string))]
    [InlineData("RequestPath", typeof(string))]
    [InlineData("HttpMethod", typeof(string))]
    [InlineData("RequestStartedAt", typeof(DateTimeOffset?))]
    [InlineData("IdempotencyKey", typeof(string))]
    [InlineData("ClientIp", typeof(string))]
    [InlineData("SessionFingerprint", typeof(string))]
    [InlineData("CurrentFingerprint", typeof(string))]
    [InlineData("RiskScore", typeof(int?))]
    [InlineData("EdgeNodeId", typeof(string))]
    [InlineData("LocaleIetfBcp47Tag", typeof(string))]
    [InlineData("TimezoneIanaName", typeof(string))]
    [InlineData("CurrencyIso4217Code", typeof(string))]
    [InlineData("OrgPlanTier", typeof(string))]
    [InlineData("FeatureFlagsCsv", typeof(string))]
    [InlineData("WhoIsHashId", typeof(string))]
    [InlineData("AdminLocationHashId", typeof(string))]
    [InlineData("City", typeof(string))]
    [InlineData("SubdivisionIso31662Code", typeof(string))]
    [InlineData("CountryIso31661Alpha2Code", typeof(string))]
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
        var authProperty = typeof(IAuthContext).GetProperty(
            propertyName, BindingFlags.Public | BindingFlags.Instance);

        authProperty.Should().NotBeNull(propertyName + " must exist on IAuthContext");

        // The inherited member is reachable via the interface map.
        var allReachable = new HashSet<string>(
            typeof(IRequestContext).GetProperties().Select(p => p.Name));
        foreach (var iface in typeof(IRequestContext).GetInterfaces())
        {
            foreach (var p in iface.GetProperties())
                allReachable.Add(p.Name);
        }

        allReachable.Should().Contain(propertyName);
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
