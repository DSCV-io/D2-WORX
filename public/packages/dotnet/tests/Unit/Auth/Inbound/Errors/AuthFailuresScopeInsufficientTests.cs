// -----------------------------------------------------------------------
// <copyright file="AuthFailuresScopeInsufficientTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Errors;

using System.Net;
using AwesomeAssertions;
using D2.Shared.Auth.Errors;
using D2.Shared.I18n;
using Xunit;

/// <summary>
/// Pins the <see cref="AuthFailures.ScopeInsufficient"/> helper. Lives in its
/// own file (alongside the broader <see cref="AuthFailuresTests"/> theory)
/// because <c>ScopeInsufficient</c> is the one helper specifically tied to the
/// per-endpoint scope-enforcement layer the AspNetCore middleware introduces;
/// keeping its pinned assertions here makes the trace from "scope insufficient
/// surfaced from middleware" → "401 with the right code" reviewable in one
/// glance.
/// </summary>
public sealed class AuthFailuresScopeInsufficientTests
{
    [Fact]
    public void ScopeInsufficient_HasUnauthorizedStatusAndScopeInsufficientErrorCode()
    {
        var result = AuthFailures.ScopeInsufficient();

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        result.ErrorCode.Should().Be(AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT);
    }

    [Fact]
    public void ScopeInsufficient_CarriesUnauthorizedTkKey()
    {
        var result = AuthFailures.ScopeInsufficient();

        result.Messages.Should().ContainSingle()
            .Which.Should().Be(TK.Auth.Errors.UNAUTHORIZED);
    }

    [Fact]
    public void ScopeInsufficientErrorCode_PinnedString()
    {
        AuthErrorCodes.AUTH_SCOPE_INSUFFICIENT.Should().Be("AUTH_SCOPE_INSUFFICIENT");
    }
}
