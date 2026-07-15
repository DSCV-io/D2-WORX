// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtLogDelegateContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Forwarding;

using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth;
using D2.Shared.Auth.Abstractions;
using D2.Shared.Auth.Outbound;
using Xunit;

/// <summary>
/// Scans every <c>[LoggerMessage]</c>-bearing <c>*Log</c> static class across the
/// loaded auth assemblies and asserts NONE declares a delegate that takes a
/// <see cref="ForwardedJwt"/> parameter. A logged <see cref="ForwardedJwt"/>
/// parameter would route the live bearer credential through the logging
/// pipeline — a forwarded JWT is never a log parameter (the fourth never-logged
/// layer). Mirrors <c>MtlsLogDelegateContractTests</c>, generalized to scan all
/// auth <c>*Log</c> classes rather than one named type.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ForwardedJwtLogDelegateContractTests
{
    [Fact]
    public void NoAuthLogDelegate_AcceptsForwardedJwtParameter()
    {
        var logTypes = AuthAssemblies()
            .SelectMany(SafeGetTypes)
            .Where(t => t.Name.EndsWith("Log", StringComparison.Ordinal)
                && t.IsClass
                && t.IsAbstract && t.IsSealed) // static class = abstract + sealed
            .ToList();

        // Guard: the scan is only meaningful if it actually found the known
        // *Log classes (AuthLog / OutboundLog). A zero-type scan would pass
        // vacuously and hide a regression.
        logTypes.Select(t => t.Name).Should().Contain("AuthLog");
        logTypes.Select(t => t.Name).Should().Contain("OutboundLog");

        var offenders = logTypes
            .SelectMany(t => t.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            .Where(m => m.GetParameters().Any(p => IsForwardedJwt(p.ParameterType)))
            .Select(m => $"{m.DeclaringType!.FullName}.{m.Name}")
            .ToList();

        offenders.Should().BeEmpty(
            "no [LoggerMessage] delegate may take a ForwardedJwt parameter — the "
            + "wrapped value is a live bearer credential. Offending delegates: "
            + string.Join(", ", offenders));
    }

    private static bool IsForwardedJwt(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t) ?? t;
        return underlying == typeof(ForwardedJwt);
    }

    private static IEnumerable<Assembly> AuthAssemblies()
    {
        // Anchor each auth assembly by a public type so it is force-loaded
        // before enumeration (assembly loading is lazy; relying on
        // AppDomain.GetAssemblies() alone is load-timing-fragile). AuthLog lives
        // in D2.Shared.Auth (anchored by AuthOptions); OutboundLog lives in
        // D2.Shared.Auth.Outbound (anchored by AuthOutboundOptions); the wrapper
        // itself lives in D2.Shared.Auth.Abstractions.
        var anchored = new[]
        {
            typeof(ForwardedJwt).Assembly,
            typeof(AuthOptions).Assembly,
            typeof(AuthOutboundOptions).Assembly,
        };

        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName().Name is { } name
                && name.StartsWith("D2.Shared.Auth", StringComparison.Ordinal))
            .Concat(anchored)
            .Distinct();
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            return ex.Types.Where(t => t is not null)!;
        }
    }
}
