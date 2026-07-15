// -----------------------------------------------------------------------
// <copyright file="LocationVoRedactionTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location;

using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Logging.Destructuring;
using Serilog.Core;
using Serilog.Events;
using Xunit;

/// <summary>
/// Verifies that the three location value objects self-redact correctly
/// through <see cref="RedactDataDestructuringPolicy"/>: each redacted property
/// renders as <c>[REDACTED: PersonalInformation]</c>, and each intentionally
/// visible property renders its real value.
/// </summary>
public sealed class LocationVoRedactionTests
{
    // -----------------------------------------------------------------------
    // Coordinates
    // -----------------------------------------------------------------------

    [Fact]
    public void Coordinates_Latitude_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006).Data!;

        policy.TryDestructure(coords, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Latitude"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void Coordinates_Longitude_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006).Data!;

        policy.TryDestructure(coords, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Longitude"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void Coordinates_PlusCode_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006).Data!;

        policy.TryDestructure(coords, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "PlusCode"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void Coordinates_HashId_IsVisibleByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006).Data!;
        var factory = new PassthroughFactory();

        policy.TryDestructure(coords, factory, out _);

        // HashId is a one-way SHA-256 digest — opaque, non-reversible, safe to log.
        factory.Recorded.Should().Contain(coords.HashId);
    }

    [Fact]
    public void Coordinates_Geohash_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006).Data!;

        policy.TryDestructure(coords, new PassthroughFactory(), out var result);

        // Geohash is a reversible spatial encoding — decoding recovers lat/lon to ~1 m.
        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Geohash"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void Coordinates_AccuracyMeters_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var coords = Coordinates.Create(40.7128, -74.006, accuracyMeters: 15.5).Data!;

        policy.TryDestructure(coords, new PassthroughFactory(), out var result);

        // AccuracyMeters combined with other context can narrow position to re-identify.
        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "AccuracyMeters"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    // -----------------------------------------------------------------------
    // StreetAddress
    // -----------------------------------------------------------------------

    [Fact]
    public void StreetAddress_Line1_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var addr = StreetAddress.Create("123 Main St").Data!;

        policy.TryDestructure(addr, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Line1"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void StreetAddress_Line2_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var addr = StreetAddress.Create("123 Main St", line2: "Apt 4B").Data!;

        policy.TryDestructure(addr, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "Line2"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void StreetAddress_HashId_IsVisibleByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var addr = StreetAddress.Create("123 Main St").Data!;
        var factory = new PassthroughFactory();

        policy.TryDestructure(addr, factory, out _);

        // HashId is a one-way SHA-256 digest — opaque, non-reversible, safe to log.
        factory.Recorded.Should().Contain(addr.HashId);
    }

    [Fact]
    public void StreetAddress_AllFiveLines_AllRedacted()
    {
        var policy = new RedactDataDestructuringPolicy();
        var addr = StreetAddress.Create(
            "123 Main St", "Apt 4B", "Floor 2", "Building C", "District 7").Data!;

        policy.TryDestructure(addr, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        var redactedPropNames = structure.Properties
            .Where(p => ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]")
            .Select(p => p.Name)
            .ToList();

        redactedPropNames.Should().Contain(["Line1", "Line2", "Line3", "Line4", "Line5"]);
    }

    // -----------------------------------------------------------------------
    // AdminLocation
    // -----------------------------------------------------------------------

    [Fact]
    public void AdminLocation_City_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var admin = AdminLocation.Create(CountryCode.US, city: "New York").Data!;

        policy.TryDestructure(admin, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "City"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void AdminLocation_PostalCode_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var admin = AdminLocation.Create(CountryCode.US, postalCode: "10001").Data!;

        policy.TryDestructure(admin, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "PostalCode"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void AdminLocation_SubdivisionIso31662Code_IsRedactedByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var sub = SubdivisionCode.FromString("US-NY");
        var admin = AdminLocation.Create(CountryCode.US, sub).Data!;

        policy.TryDestructure(admin, new PassthroughFactory(), out var result);

        var structure = result.Should().BeOfType<StructureValue>().Subject;
        structure.Properties.Should().Contain(p =>
            p.Name == "SubdivisionIso31662Code"
            && ((ScalarValue)p.Value).Value!.ToString() == "[REDACTED: PersonalInformation]");
    }

    [Fact]
    public void AdminLocation_HashId_IsVisibleByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var admin = AdminLocation.Create(CountryCode.US, city: "New York").Data!;
        var factory = new PassthroughFactory();

        policy.TryDestructure(admin, factory, out _);

        // HashId is a one-way SHA-256 digest — opaque, non-reversible, safe to log.
        factory.Recorded.Should().Contain(admin.HashId);
    }

    [Fact]
    public void AdminLocation_CountryIso31661Alpha2Code_IsVisibleByPolicy()
    {
        var policy = new RedactDataDestructuringPolicy();
        var admin = AdminLocation.Create(CountryCode.US, city: "New York").Data!;
        var factory = new PassthroughFactory();

        policy.TryDestructure(admin, factory, out _);

        // CountryIso31661Alpha2Code is not redacted — forwarded to factory.
        factory.Recorded.Should().Contain(CountryCode.US);
    }

    // -----------------------------------------------------------------------
    // Stub ILogEventPropertyValueFactory
    // -----------------------------------------------------------------------

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
