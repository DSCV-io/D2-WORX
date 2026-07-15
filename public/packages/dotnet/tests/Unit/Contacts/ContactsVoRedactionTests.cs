// -----------------------------------------------------------------------
// <copyright file="ContactsVoRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Contacts;

using AwesomeAssertions;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.Logging.Destructuring;
using DcsvIo.D2.Validation.Abstractions;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Verifies that the six contacts value objects self-redact correctly through
/// <see cref="RedactDataDestructuringPolicy"/>: each PII property renders as
/// <c>[REDACTED: PersonalInformation]</c>, and each intentionally visible
/// property (HashId, affix enums, company website) renders its real value.
/// </summary>
public sealed class ContactsVoRedactionTests
{
    private const string _REDACTED = "[REDACTED: PersonalInformation]";

    // -----------------------------------------------------------------------
    // Personal
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("FirstName")]
    [InlineData("MiddleName")]
    [InlineData("LastName")]
    [InlineData("PreferredName")]
    public void Personal_NameProperties_AreRedactedByPolicy(string propertyName)
    {
        var personal = Personal.Create("John", "Quincy", "Adams", "JQ").Data!;
        var structure = Destructure(personal);

        structure.Properties.Should().Contain(p =>
            p.Name == propertyName
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    [Fact]
    public void Personal_HashId_IsVisibleByPolicy()
    {
        var personal = Personal.Create("John", "Quincy", "Adams").Data!;
        var factory = new PassthroughFactory();

        new RedactDataDestructuringPolicy().TryDestructure(personal, factory, out _);

        // HashId is a one-way SHA-256 digest — opaque, non-reversible, safe to log.
        factory.Recorded.Should().Contain(personal.HashId);
    }

    // -----------------------------------------------------------------------
    // NameAffixes
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("PrefixCustom")]
    [InlineData("SuffixCustom")]
    public void NameAffixes_CustomProperties_AreRedactedByPolicy(string propertyName)
    {
        var affixes = NameAffixes.Create(
            NamePrefix.Other, "Captain", NameSuffix.Other, "Ret.").Data!;
        var structure = Destructure(affixes);

        structure.Properties.Should().Contain(p =>
            p.Name == propertyName
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    [Fact]
    public void NameAffixes_Enums_AreVisibleByPolicy()
    {
        var affixes = NameAffixes.Create(NamePrefix.Dr, suffix: NameSuffix.Jr).Data!;
        var factory = new PassthroughFactory();

        new RedactDataDestructuringPolicy().TryDestructure(affixes, factory, out _);

        factory.Recorded.Should().Contain(NamePrefix.Dr);
        factory.Recorded.Should().Contain(NameSuffix.Jr);
    }

    // -----------------------------------------------------------------------
    // Demographics
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("DateOfBirth")]
    [InlineData("BiologicalSex")]
    public void Demographics_Properties_AreRedactedByPolicy(string propertyName)
    {
        var demographics = Demographics.Create(
            new NodaTime.LocalDate(1990, 3, 15), BiologicalSex.Female).Data!;
        var structure = Destructure(demographics);

        structure.Properties.Should().Contain(p =>
            p.Name == propertyName
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    // -----------------------------------------------------------------------
    // Professional
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("CompanyName")]
    [InlineData("JobTitle")]
    [InlineData("Department")]
    public void Professional_TextProperties_AreRedactedByPolicy(string propertyName)
    {
        var professional = Professional.Create(
            "DCSV", "Engineer", "Platform", "https://example.com").Data!;
        var structure = Destructure(professional);

        structure.Properties.Should().Contain(p =>
            p.Name == propertyName
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    [Fact]
    public void Professional_CompanyWebsite_IsVisibleByPolicy()
    {
        var professional = Professional.Create(
            "DCSV", companyWebsite: "https://example.com").Data!;
        var factory = new PassthroughFactory();

        new RedactDataDestructuringPolicy().TryDestructure(professional, factory, out _);

        // A public website URL is not individually identifying — left visible.
        factory.Recorded.Should().Contain(professional.CompanyWebsite);
    }

    // -----------------------------------------------------------------------
    // EmailAddress / PhoneNumber
    // -----------------------------------------------------------------------

    [Fact]
    public void EmailAddress_Value_IsRedactedByPolicy()
    {
        var email = EmailAddress.Create("user@example.com").Data!;
        var structure = Destructure(email);

        structure.Properties.Should().Contain(p =>
            p.Name == "Value"
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    [Fact]
    public void PhoneNumber_Value_IsRedactedByPolicy()
    {
        var phone = PhoneNumber.Create("+1 212 555 1234").Data!;
        var structure = Destructure(phone);

        structure.Properties.Should().Contain(p =>
            p.Name == "Value"
            && ((ScalarValue)p.Value).Value!.ToString() == _REDACTED);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static StructureValue Destructure(object value)
    {
        new RedactDataDestructuringPolicy()
            .TryDestructure(value, new PassthroughFactory(), out var result);
        return result.Should().BeOfType<StructureValue>().Subject;
    }

    /// <summary>
    /// Stub <see cref="ILogEventPropertyValueFactory"/> that records all
    /// non-redacted values forwarded by the policy, enabling assertions that
    /// visible properties reached the factory with their real values.
    /// </summary>
    private sealed class PassthroughFactory : ILogEventPropertyValueFactory
    {
        public List<object?> Recorded { get; } = [];

        public LogEventPropertyValue CreatePropertyValue(
            object? value,
            bool destructureObjects = false)
        {
            Recorded.Add(value);
            return new ScalarValue(value);
        }
    }
}
