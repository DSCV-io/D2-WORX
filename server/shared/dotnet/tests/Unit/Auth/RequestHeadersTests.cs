// -----------------------------------------------------------------------
// <copyright file="RequestHeadersTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

public sealed class RequestHeadersTests
{
    [Fact]
    public void IdempotencyKey_HasConventionalStripeStyleName()
    {
        // Adversarial: Idempotency-Key is a Stripe-conventional name; using
        // X-D2-Idempotency-Key would needlessly break every standard idempotency
        // client.
        RequestHeaders.IDEMPOTENCY_KEY.Should().Be("Idempotency-Key");
    }

    [Fact]
    public void ClientFingerprint_UsesXD2Prefix()
    {
        RequestHeaders.CLIENT_FINGERPRINT.Should().Be("X-D2-Client-Fingerprint");
    }

    [Fact]
    public void EveryD2SpecificHeader_StartsWithXD2Prefix()
    {
        // Adversarial: catch future additions that forget the X-D2- prefix
        // discipline. The ONLY exempt header is Idempotency-Key (well-known
        // industry convention).
        string[] conventionalExempt = ["Idempotency-Key"];

        var d2Specific = typeof(RequestHeaders)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .Where(v => !conventionalExempt.Contains(v))
            .ToList();

        d2Specific.Should().NotBeEmpty();
        d2Specific.Should().AllSatisfy(h =>
            h.Should().StartWith("X-D2-", "every D²-custom header must use the X-D2- prefix"));
    }

    [Fact]
    public void HeaderConstants_AreNonEmptyTrimmedStrings()
    {
        var allHeaders = typeof(RequestHeaders)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .ToList();

        allHeaders.Should().NotBeEmpty();
        allHeaders.Should().AllSatisfy(h =>
        {
            h.Should().NotBeNullOrWhiteSpace();
            h.Should().Be(h.Trim(), "header constants must not carry surrounding whitespace");
        });
    }
}
