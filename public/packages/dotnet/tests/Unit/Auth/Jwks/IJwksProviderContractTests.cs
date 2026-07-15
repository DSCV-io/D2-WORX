// -----------------------------------------------------------------------
// <copyright file="IJwksProviderContractTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth.Jwks;

using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions.Jwks;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Contract pinning for <see cref="IJwksProvider"/>. The interface is the
/// versioned contract every consumer-side service depends on; signature
/// drift here is a breaking change. Failures here mean any downstream impl
/// (the in-tree <c>HttpJwksProvider</c> + Edge's issuer-side impl) will
/// fail to compile — that's the desired surface, but we want the regression
/// to land at the abstractions test layer first.
/// </summary>
public sealed class IJwksProviderContractTests
{
    [Fact]
    public void GetKeysAsync_HasExpectedShape()
    {
        var method = typeof(IJwksProvider).GetMethod(nameof(IJwksProvider.GetKeysAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask<D2Result<JwksKeySetSnapshot>>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].HasDefaultValue.Should().BeTrue(
            "GetKeysAsync's CancellationToken should default to default(CancellationToken)");
    }

    [Fact]
    public void RefreshAsync_HasExpectedShape()
    {
        var method = typeof(IJwksProvider).GetMethod(nameof(IJwksProvider.RefreshAsync));

        method.Should().NotBeNull();
        method.ReturnType.Should().Be<ValueTask<D2Result>>();

        var parameters = method.GetParameters();
        parameters.Should().HaveCount(1);
        parameters[0].ParameterType.Should().Be<CancellationToken>();
        parameters[0].HasDefaultValue.Should().BeTrue(
            "RefreshAsync's CancellationToken should default to default(CancellationToken)");
    }

    [Fact]
    public void Interface_IsPublic()
    {
        typeof(IJwksProvider).IsPublic.Should().BeTrue();
        typeof(IJwksProvider).IsInterface.Should().BeTrue();
    }

    [Fact]
    public void Interface_HasExactlyTwoMethods()
    {
        // Adversarial: catches accidental method additions / removals on the
        // contract surface. A future expansion is fine — bump this count
        // intentionally and add a corresponding shape test.
        var methods = typeof(IJwksProvider).GetMethods(
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        methods.Should().HaveCount(2);
    }
}
