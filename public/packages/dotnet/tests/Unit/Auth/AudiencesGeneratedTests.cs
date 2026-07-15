// -----------------------------------------------------------------------
// <copyright file="AudiencesGeneratedTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using Xunit;

/// <summary>
/// Public-half <c>Audiences</c> catalog smoke tests (empty stub after product split).
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
    public void Audiences_PublicCatalog_IsEmptyStub()
    {
        Audiences.AllUrls.Should().BeEmpty(
            "product service audiences moved to private ProductAudiences");
        Audiences.ByName.Should().BeEmpty();
    }

    [Theory]
    [InlineData("https://files.internal")]
    [InlineData("https://notifications.internal")]
    [InlineData("https://courier.internal")]
    [InlineData("https://audit.internal")]
    public void IsKnown_ProductUrls_AreNotInPublicCatalog(string url)
    {
        Audiences.IsKnown(url).Should().BeFalse();
    }

    [Fact]
    public void IsKnown_EmptyString_ReturnsFalse()
    {
        Audiences.IsKnown(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void Resolve_UnknownName_ReturnsNull()
    {
        Audiences.Resolve("Files").Should().BeNull();
        Audiences.Resolve("not-a-real-audience").Should().BeNull();
    }
}
