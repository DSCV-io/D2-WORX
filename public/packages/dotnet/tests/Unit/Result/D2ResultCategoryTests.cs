// -----------------------------------------------------------------------
// <copyright file="D2ResultCategoryTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Result;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.ErrorCodes.Category;
using DcsvIo.D2.Result;
using Xunit;

/// <summary>
/// Pins the typed <see cref="D2Result.Category"/> field — that every
/// spec-derived semantic factory stamps the <see cref="ErrorCategory"/>
/// declared for its code in
/// <c>contracts/error-codes/error-codes.spec.json</c> (baked at generation
/// time, no runtime registry lookup), that the hand-rolled success / raw
/// factories carry no category, and that the field round-trips through the
/// constructor, <see cref="D2Result.WithTraceId"/>, and the
/// <c>Bubble</c> / <c>BubbleFail</c> propagation factories.
/// </summary>
public sealed class D2ResultCategoryTests
{
    [Fact]
    public void NotFound_CarriesNotFoundCategory()
        => D2Result.NotFound().Category.Should().Be(ErrorCategory.NotFound);

    [Fact]
    public void Forbidden_CarriesPolicyDeniedCategory()
        => D2Result.Forbidden().Category.Should().Be(ErrorCategory.PolicyDenied);

    [Fact]
    public void Unauthorized_CarriesPolicyDeniedCategory()
        => D2Result.Unauthorized().Category.Should().Be(ErrorCategory.PolicyDenied);

    [Fact]
    public void ValidationFailed_CarriesValidationFailureCategory()
        => D2Result.ValidationFailed().Category.Should().Be(ErrorCategory.ValidationFailure);

    [Fact]
    public void Conflict_CarriesConflictCategory()
        => D2Result.Conflict().Category.Should().Be(ErrorCategory.Conflict);

    [Fact]
    public void UnhandledException_CarriesInternalErrorCategory()
        => D2Result.UnhandledException().Category.Should().Be(ErrorCategory.InternalError);

    [Fact]
    public void ServiceUnavailable_CarriesInfrastructureUnavailableCategory()
        => D2Result.ServiceUnavailable().Category
            .Should().Be(ErrorCategory.InfrastructureUnavailable);

    [Fact]
    public void TooManyRequests_CarriesRateLimitedCategory()
        => D2Result.TooManyRequests().Category.Should().Be(ErrorCategory.RateLimited);

    [Fact]
    public void PayloadTooLarge_CarriesPayloadTooLargeCategory()
        => D2Result.PayloadTooLarge().Category.Should().Be(ErrorCategory.PayloadTooLarge);

    [Fact]
    public void Canceled_CarriesValidationFailureCategory()
        => D2Result.Canceled().Category.Should().Be(ErrorCategory.ValidationFailure);

    [Theory]
    [InlineData("not_found", ErrorCategory.NotFound)]
    [InlineData("policy_denied", ErrorCategory.PolicyDenied)]
    [InlineData("validation_failure", ErrorCategory.ValidationFailure)]
    [InlineData("conflict", ErrorCategory.Conflict)]
    [InlineData("internal_error", ErrorCategory.InternalError)]
    [InlineData("infrastructure_unavailable", ErrorCategory.InfrastructureUnavailable)]
    [InlineData("rate_limited", ErrorCategory.RateLimited)]
    [InlineData("payload_too_large", ErrorCategory.PayloadTooLarge)]
    public void GenericTwin_StampsSameCategoryAsNonGeneric(
        string wire, ErrorCategory expected)
    {
        // The <TData> twin of each base factory stamps the identical category;
        // pin the wire string against the typed enum to catch any divergence
        // between the generic + non-generic emit passes.
        var category = wire switch
        {
            "not_found" => D2Result<string>.NotFound().Category,
            "policy_denied" => D2Result<string>.Forbidden().Category,
            "validation_failure" => D2Result<string>.ValidationFailed().Category,
            "conflict" => D2Result<string>.Conflict().Category,
            "internal_error" => D2Result<string>.UnhandledException().Category,
            "infrastructure_unavailable" => D2Result<string>.ServiceUnavailable().Category,
            "rate_limited" => D2Result<string>.TooManyRequests().Category,
            "payload_too_large" => D2Result<string>.PayloadTooLarge().Category,
            _ => null,
        };

        category.Should().Be(expected);
    }

    [Fact]
    public void Forbidden_CategoryOverrideWins()
    {
        // Every error factory (the universal standard shape) exposes a category
        // override so a delegating domain factory can stamp its own code's
        // category onto the base factory.
        var result = D2Result.Forbidden(category: ErrorCategory.ValidationFailure);

        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void SomeFound_CarriesPartialSuccessCategory()
        => D2Result.SomeFound().Category.Should().Be(ErrorCategory.PartialSuccess);

    [Fact]
    public void SomeFound_Generic_CarriesPartialSuccessCategory()
        => D2Result<string>.SomeFound().Category.Should().Be(ErrorCategory.PartialSuccess);

    [Fact]
    public void PartialSuccess_CarriesPartialSuccessCategory()
        => D2Result<string>.PartialSuccess().Category.Should().Be(ErrorCategory.PartialSuccess);

    [Fact]
    public void Ok_HasNoCategory()
        => D2Result.Ok().Category.Should().BeNull();

    [Fact]
    public void Created_HasNoCategory()
        => D2Result.Created().Category.Should().BeNull();

    [Fact]
    public void Fail_WithoutCategory_HasNoCategory()
        => D2Result.Fail().Category.Should().BeNull();

    [Fact]
    public void Constructor_RoundTripsCategory()
    {
        var result = new D2Result(
            false,
            statusCode: HttpStatusCode.NotFound,
            errorCode: "X",
            category: ErrorCategory.NotFound);

        result.Category.Should().Be(ErrorCategory.NotFound);
    }

    [Fact]
    public void GenericConstructor_RoundTripsCategory()
    {
        var result = new D2Result<int>(
            false,
            data: default,
            category: ErrorCategory.Conflict);

        result.Category.Should().Be(ErrorCategory.Conflict);
    }

    [Fact]
    public void WithTraceId_PreservesCategory()
    {
        var result = D2Result.NotFound().WithTraceId("0123456789abcdef0123456789abcdef");

        result.Category.Should().Be(ErrorCategory.NotFound);
        result.TraceId.Should().Be("0123456789abcdef0123456789abcdef");
    }

    [Fact]
    public void GenericWithTraceId_PreservesCategory()
    {
        var result = D2Result<string>.ValidationFailed()
            .WithTraceId("0123456789abcdef0123456789abcdef");

        result.Category.Should().Be(ErrorCategory.ValidationFailure);
    }

    [Fact]
    public void BubbleFail_PreservesUpstreamCategory()
    {
        var upstream = D2Result.Conflict();

        var bubbled = D2Result<string>.BubbleFail(upstream);

        bubbled.Category.Should().Be(ErrorCategory.Conflict);
    }

    [Fact]
    public void Bubble_PreservesUpstreamCategory()
    {
        var upstream = D2Result.NotFound();

        var bubbled = D2Result<string>.Bubble(upstream);

        bubbled.Category.Should().Be(ErrorCategory.NotFound);
    }
}
