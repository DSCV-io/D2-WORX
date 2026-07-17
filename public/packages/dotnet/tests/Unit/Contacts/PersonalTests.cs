// -----------------------------------------------------------------------
// <copyright file="PersonalTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using System.Security.Cryptography;
using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.I18n;
using DcsvIo.D2.Validation.Abstractions;
using Xunit;

/// <summary>
/// Adversarial coverage for <see cref="Personal"/>: first-name-required floor,
/// per-field length caps, and the HashId invariants — determinism,
/// case / diacritic / whitespace collapse, and preferred-name exclusion.
/// </summary>
public sealed class PersonalTests
{
    // -----------------------------------------------------------------------
    // Happy path
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_AllNames_ReturnsOk_WithCleanedValues()
    {
        var result = Personal.Create("  John  ", "  Quincy ", " Adams ", " JQ ");

        result.Success.Should().BeTrue();
        var personal = result.Data!;
        personal.FirstName.Should().Be("John");
        personal.MiddleName.Should().Be("Quincy");
        personal.LastName.Should().Be("Adams");
        personal.PreferredName.Should().Be("JQ");
        personal.HashId.Should().StartWith("v1.");
        personal.HashId.Length.Should().Be("v1.".Length + (SHA256.HashSizeInBytes * 2));
    }

    [Fact]
    public void Create_FirstNameOnly_ReturnsOk_OptionalsNull()
    {
        var result = Personal.Create("Jane");

        result.Success.Should().BeTrue();
        result.Data!.FirstName.Should().Be("Jane");
        result.Data!.MiddleName.Should().BeNull();
        result.Data!.LastName.Should().BeNull();
        result.Data!.PreferredName.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // FirstName required
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Create_FirstNameMissing_ReturnsFirstNameRequired(string? firstName)
    {
        var result = Personal.Create(firstName);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.FIRST_NAME_REQUIRED.Key);
    }

    // -----------------------------------------------------------------------
    // Length caps (255 max each)
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_FirstNameExactlyMax_ReturnsOk()
    {
        var atMax = new string('a', FieldConstraints.FIRST_NAME_MAX);
        var result = Personal.Create(atMax);

        result.Success.Should().BeTrue();
        result.Data!.FirstName.Should().HaveLength(FieldConstraints.FIRST_NAME_MAX);
    }

    [Fact]
    public void Create_FirstNameOverMax_ReturnsFirstNameTooLong()
    {
        var overMax = new string('a', FieldConstraints.FIRST_NAME_MAX + 1);
        var result = Personal.Create(overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.FIRST_NAME_TOO_LONG.Key);
    }

    [Fact]
    public void Create_MiddleNameOverMax_ReturnsMiddleNameTooLong()
    {
        var overMax = new string('b', FieldConstraints.MIDDLE_NAME_MAX + 1);
        var result = Personal.Create("John", overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.MIDDLE_NAME_TOO_LONG.Key);
    }

    [Fact]
    public void Create_LastNameOverMax_ReturnsLastNameTooLong()
    {
        var overMax = new string('c', FieldConstraints.LAST_NAME_MAX + 1);
        var result = Personal.Create("John", lastName: overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.LAST_NAME_TOO_LONG.Key);
    }

    [Fact]
    public void Create_PreferredNameOverMax_ReturnsPreferredNameTooLong()
    {
        var overMax = new string('d', FieldConstraints.PREFERRED_NAME_MAX + 1);
        var result = Personal.Create("John", preferredName: overMax);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Contacts.Validation.PREFERRED_NAME_TOO_LONG.Key);
    }

    [Fact]
    public void Create_MiddleNameExactlyMax_ReturnsOk()
    {
        var atMax = new string('e', FieldConstraints.MIDDLE_NAME_MAX);
        var result = Personal.Create("John", atMax);

        result.Success.Should().BeTrue();
        result.Data!.MiddleName.Should().HaveLength(FieldConstraints.MIDDLE_NAME_MAX);
    }

    [Fact]
    public void Create_LastNameExactlyMax_ReturnsOk()
    {
        var atMax = new string('e', FieldConstraints.LAST_NAME_MAX);
        var result = Personal.Create("John", lastName: atMax);

        result.Success.Should().BeTrue();
        result.Data!.LastName.Should().HaveLength(FieldConstraints.LAST_NAME_MAX);
    }

    [Fact]
    public void Create_PreferredNameExactlyMax_ReturnsOk()
    {
        var atMax = new string('e', FieldConstraints.PREFERRED_NAME_MAX);
        var result = Personal.Create("John", preferredName: atMax);

        result.Success.Should().BeTrue();
        result.Data!.PreferredName.Should().HaveLength(FieldConstraints.PREFERRED_NAME_MAX);
    }

    // -----------------------------------------------------------------------
    // HashId — shape
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_MatchesV1HexShape()
    {
        var hashId = Personal.Create("John", "Quincy", "Adams").Data!.HashId;

        hashId.Should().StartWith("v1.");
        hashId.Should().HaveLength("v1.".Length + (SHA256.HashSizeInBytes * 2));
        var hex = hashId["v1.".Length..];
        hex.Should().HaveLength(SHA256.HashSizeInBytes * 2);
        hex.Should().MatchRegex("^[0-9a-f]+$");
    }

    // -----------------------------------------------------------------------
    // HashId — determinism
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_Deterministic_AcrossCalls()
    {
        var r1 = Personal.Create("John", "Quincy", "Adams");
        var r2 = Personal.Create("John", "Quincy", "Adams");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_DiffersByFirstName()
    {
        var r1 = Personal.Create("John");
        var r2 = Personal.Create("Jane");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // HashId — case / diacritic / whitespace canonicalization
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_CaseInsensitive()
    {
        var r1 = Personal.Create("José");
        var r2 = Personal.Create("JOSÉ");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_DiacriticInsensitive()
    {
        var r1 = Personal.Create("José");
        var r2 = Personal.Create("Jose");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_WhitespaceTrimInvariant()
    {
        var r1 = Personal.Create("  John  ");
        var r2 = Personal.Create("John");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // HashId — preferred name excluded
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_ExcludesPreferredName()
    {
        var r1 = Personal.Create("John", "Quincy", "Adams", "Johnny");
        var r2 = Personal.Create("John", "Quincy", "Adams", "JQ");

        r1.Data!.HashId.Should().Be(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_DiffersByMiddleName()
    {
        var r1 = Personal.Create("John", "Quincy");
        var r2 = Personal.Create("John", "Fitzgerald");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    [Fact]
    public void Create_HashId_DiffersByLastName()
    {
        var r1 = Personal.Create("John", "Quincy", "Adams");
        var r2 = Personal.Create("John", "Quincy", "Kennedy");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }

    // -----------------------------------------------------------------------
    // HashId — positional slot collision (the | separator must prevent
    // First|Middle|Last slot collision: ("A","B",null) vs ("A",null,"B"))
    // -----------------------------------------------------------------------

    [Fact]
    public void Create_HashId_PositionalSlotCollision_DifferentHashIds()
    {
        // Without the "|" separator the hash inputs would be identical:
        // "A" + null + null + "B" → "AB" (either way).
        // With "|": "A|B|" vs "A||B" → distinct byte sequences → distinct hashes.
        var r1 = Personal.Create("A", "B");
        var r2 = Personal.Create("A", null, "B");

        r1.Data!.HashId.Should().NotBe(r2.Data!.HashId);
    }
}
