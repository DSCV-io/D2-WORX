// -----------------------------------------------------------------------
// <copyright file="AudiencesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using AwesomeAssertions;
using D2.Shared.Auth.Abstractions;
using Xunit;

/// <summary>
/// End-to-end smoke tests for the codegen-emitted <c>Audiences.g.cs</c> static
/// partial class. Probes structure, specific constants, helper behavior, and
/// the read-only collection projections.
/// </summary>
public sealed class AudiencesGeneratedTests
{
    [Fact]
    public void Audiences_TypeExists()
    {
        var audiencesType = typeof(Audiences);

        audiencesType.Should().NotBeNull();
        audiencesType.IsAbstract.Should().BeTrue("static classes are abstract+sealed at IL");
        audiencesType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void Audiences_SpecificConstants_HaveExpectedUrls()
    {
        // Pin the spec→codegen wiring to known good values. Updating any of
        // these IS a breaking change for anything reading the JWT aud claim.
        Audiences.Files.Should().Be("https://files.internal");
        Audiences.Notifications.Should().Be("https://notifications.internal");
        Audiences.Courier.Should().Be("https://courier.internal");
        Audiences.Audit.Should().Be("https://audit.internal");
    }

    // ----------------------------------------------------------------------
    // Helper: IsKnown
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("https://files.internal", true)]
    [InlineData("https://notifications.internal", true)]
    [InlineData("https://courier.internal", true)]
    [InlineData("https://audit.internal", true)]
    [InlineData("https://unknown.internal", false)]
    [InlineData("https://files.internal/", false)] // trailing slash differs
    [InlineData("HTTPS://FILES.INTERNAL", false)] // case-sensitive
    public void IsKnown_OnlyMatchesSpecAudiences(string url, bool expected)
    {
        Audiences.IsKnown(url).Should().Be(expected);
    }

    [Fact]
    public void IsKnown_EmptyString_ReturnsFalse()
    {
        Audiences.IsKnown(string.Empty).Should().BeFalse();
    }

    // ----------------------------------------------------------------------
    // Helper: Resolve (name → url)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("Files", "https://files.internal")]
    [InlineData("Notifications", "https://notifications.internal")]
    [InlineData("Courier", "https://courier.internal")]
    [InlineData("Audit", "https://audit.internal")]
    public void Resolve_KnownName_ReturnsUrl(string name, string expected)
    {
        Audiences.Resolve(name).Should().Be(expected);
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Audiences.Resolve("NotARealAudience").Should().BeNull();
        Audiences.Resolve(string.Empty).Should().BeNull();
    }

    [Fact]
    public void Resolve_CaseSensitive()
    {
        // Adversarial: audience names are case-sensitive — "files" must NOT
        // resolve like "Files".
        Audiences.Resolve("files").Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Helper: ResolveByUrl (url → name)
    // ----------------------------------------------------------------------

    [Theory]
    [InlineData("https://files.internal", "Files")]
    [InlineData("https://notifications.internal", "Notifications")]
    [InlineData("https://courier.internal", "Courier")]
    [InlineData("https://audit.internal", "Audit")]
    public void ResolveByUrl_KnownUrl_ReturnsName(string url, string expected)
    {
        Audiences.ResolveByUrl(url).Should().Be(expected);
    }

    [Fact]
    public void ResolveByUrl_UnknownUrl_ReturnsNull()
    {
        Audiences.ResolveByUrl("https://nope.internal").Should().BeNull();
        Audiences.ResolveByUrl(string.Empty).Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Collection projections
    // ----------------------------------------------------------------------

    [Fact]
    public void AllUrls_ContainsEverySpecEntry()
    {
        Audiences.AllUrls.Should().BeEquivalentTo(
        [
            "https://files.internal",
            "https://notifications.internal",
            "https://courier.internal",
            "https://audit.internal",
        ]);
    }

    [Fact]
    public void ByName_ContainsEverySpecEntry()
    {
        Audiences.ByName.Should().HaveCount(4);
        Audiences.ByName["Files"].Should().Be("https://files.internal");
        Audiences.ByName["Notifications"].Should().Be("https://notifications.internal");
        Audiences.ByName["Courier"].Should().Be("https://courier.internal");
        Audiences.ByName["Audit"].Should().Be("https://audit.internal");
    }

    [Fact]
    public void AllUrls_IsReadOnly()
    {
        // Adversarial: callers must not be able to mutate the URL set at
        // runtime. Any editable surface would be a security hole — a rogue
        // handler could register a fake audience and bypass aud validation.
        Audiences.AllUrls.Should().BeAssignableTo<IReadOnlySet<string>>();
    }

    [Fact]
    public void ByName_IsReadOnly()
    {
        Audiences.ByName.Should().BeAssignableTo<IReadOnlyDictionary<string, string>>();
    }

    // ----------------------------------------------------------------------
    // Cross-projection consistency
    // ----------------------------------------------------------------------

    [Fact]
    public void Resolve_RoundTripsThroughResolveByUrl()
    {
        // Every name maps to a URL that maps back to the same name.
        foreach (var (name, url) in Audiences.ByName)
        {
            Audiences.Resolve(name).Should().Be(url);
            Audiences.ResolveByUrl(url).Should().Be(name);
        }
    }

    [Fact]
    public void AllUrls_MatchesByNameValues()
    {
        // Sanity: AllUrls should be exactly the values in ByName.
        Audiences.AllUrls.Should().BeEquivalentTo(Audiences.ByName.Values);
    }
}
