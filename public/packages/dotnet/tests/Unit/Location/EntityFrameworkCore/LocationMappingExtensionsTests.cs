// -----------------------------------------------------------------------
// <copyright file="LocationMappingExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Location.EntityFrameworkCore;

using System.Security.Cryptography;
using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.Abstractions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location.EntityFrameworkCore;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Unit tests for <see cref="LocationMappingExtensions"/>. Exercises every public helper
/// (<c>MapStreetAddress</c>, <c>MapAdminLocation</c>, <c>MapCoordinates</c>) via
/// <c>ModelBuilder</c> + built-<c>IModel</c> introspection (model-build-only; no live DB).
/// </summary>
[Trait("Category", "Unit")]
public sealed class LocationMappingExtensionsTests
{
    private static readonly int sr_hashIdMax = 3 + (SHA256.HashSizeInBytes * 2);

    private static readonly string sr_hashIdCleared =
        "v1." + new string('0', SHA256.HashSizeInBytes * 2);

    // =========================================================================
    // MapStreetAddress
    // =========================================================================

    [Fact]
    public void MapStreetAddress_Line1_has_correct_max_length_and_Constant_anonymize()
    {
        using var ctx = StreetContext.Build();
        var prop = ComplexProp<StreetEntity>(ctx.Model, "Address", nameof(StreetAddress.Line1));
        prop.GetMaxLength().Should().Be(FieldConstraints.STREET_LINE_MAX);
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("[deleted]");
    }

    [Fact]
    public void MapStreetAddress_Line2_has_max_length_and_SetNull()
    {
        using var ctx = StreetContext.Build();
        var prop = ComplexProp<StreetEntity>(ctx.Model, "Address", nameof(StreetAddress.Line2));
        prop.GetMaxLength().Should().Be(FieldConstraints.STREET_LINE_MAX);
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapStreetAddress_Line3_to_Line5_have_max_length_and_SetNull()
    {
        using var ctx = StreetContext.Build();
        foreach (var fieldName in new[]
        {
            nameof(StreetAddress.Line3),
            nameof(StreetAddress.Line4),
            nameof(StreetAddress.Line5),
        })
        {
            var prop = ComplexProp<StreetEntity>(ctx.Model, "Address", fieldName);
            prop.GetMaxLength().Should().Be(
                FieldConstraints.STREET_LINE_MAX,
                because: $"{fieldName} should have STREET_LINE_MAX");
            AnonRule(prop)!.Kind.Should().Be(
                AnonymizeKind.SetNull,
                because: $"{fieldName} should be SetNull");
        }
    }

    [Fact]
    public void MapStreetAddress_HashId_has_correct_max_length_and_cleared_sentinel()
    {
        using var ctx = StreetContext.Build();
        var prop = ComplexProp<StreetEntity>(ctx.Model, "Address", nameof(StreetAddress.HashId));
        prop.GetMaxLength().Should().Be(sr_hashIdMax);
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(sr_hashIdCleared);
    }

    [Fact]
    public void MapStreetAddress_maps_as_complex_property_on_entity()
    {
        using var ctx = StreetContext.Build();
        ctx.Model.FindEntityType(typeof(StreetEntity))!
            .FindComplexProperty("Address")
            .Should().NotBeNull("MapStreetAddress should map StreetAddress as a ComplexProperty");
    }

    // =========================================================================
    // MapAdminLocation
    // =========================================================================

    [Fact]
    public void MapAdminLocation_City_has_correct_max_length_and_SetNull()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(ctx.Model, "Location", nameof(AdminLocation.City));
        prop.GetMaxLength().Should().Be(FieldConstraints.CITY_MAX);
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapAdminLocation_PostalCode_has_correct_max_length_and_SetNull()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.PostalCode));
        prop.GetMaxLength().Should().Be(FieldConstraints.POSTAL_CODE_MAX);
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapAdminLocation_Subdivision_has_correct_max_length_converter_and_SetNull()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.SubdivisionIso31662Code));
        prop.GetMaxLength().Should().Be(LocationVoDecorator.SubdivisionCodeMax);
        prop.GetValueConverter().Should().NotBeNull(
            because: "SubdivisionCode should have a struct↔string converter");
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapAdminLocation_Country_has_no_anonymize_annotation()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.CountryIso31661Alpha2Code));
        AnonRule(prop).Should().BeNull(because: "Country is kept on erasure — no annotation");
    }

    [Fact]
    public void MapAdminLocation_Country_has_string_converter_and_max_length()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.CountryIso31661Alpha2Code));

        // HasConversion<string>() on the CountryCode enum resolves the built-in
        // EnumToStringConverter via the type mapping (so GetValueConverter() is null —
        // that surface reports only CUSTOM converters). Assert the provider type instead:
        // the column is a string, and the 2-char alpha-2 cap is applied.
        prop.GetProviderClrType().Should().Be<string>(
            because: "CountryCode enum maps to an alpha-2 string column");
        prop.GetMaxLength().Should().Be(LocationVoDecorator.CountryCodeMax);
    }

    [Fact]
    public void MapAdminLocation_HashId_has_correct_max_length_and_cleared_sentinel()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.HashId));
        prop.GetMaxLength().Should().Be(sr_hashIdMax);
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(sr_hashIdCleared);
    }

    [Fact]
    public void MapAdminLocation_Subdivision_converter_maps_null_and_code_correctly()
    {
        using var ctx = AdminContext.Build();
        var prop = ComplexProp<AdminEntity>(
            ctx.Model, "Location", nameof(AdminLocation.SubdivisionIso31662Code));
        var conv = prop.GetValueConverter()!;

        conv.ConvertToProvider(null).Should().BeNull();
        conv.ConvertFromProvider(null).Should().BeNull();

        var code = SubdivisionCode.FromString("US-CA");
        conv.ConvertToProvider(code).Should().Be("US-CA");
        conv.ConvertFromProvider("US-CA").Should().Be(code);

        conv.ConvertFromProvider(string.Empty).Should().BeNull(
            because: "an empty stored value must materialize as null");
    }

    [Fact]
    public void MapAdminLocation_maps_as_complex_property_on_entity()
    {
        using var ctx = AdminContext.Build();
        ctx.Model.FindEntityType(typeof(AdminEntity))!
            .FindComplexProperty("Location")
            .Should().NotBeNull("MapAdminLocation should map AdminLocation as a ComplexProperty");
    }

    // =========================================================================
    // MapCoordinates
    // =========================================================================

    [Fact]
    public void MapCoordinates_Latitude_has_no_length_and_Constant_zero()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.Latitude));
        prop.GetMaxLength().Should().BeNull(because: "Latitude is a numeric column — no length");
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("0");
    }

    [Fact]
    public void MapCoordinates_Longitude_has_no_length_and_Constant_zero()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.Longitude));
        prop.GetMaxLength().Should().BeNull(because: "Longitude is a numeric column — no length");
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be("0");
    }

    [Fact]
    public void MapCoordinates_Geohash_has_correct_max_length_and_SetEmpty()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.Geohash));
        prop.GetMaxLength().Should().Be(LocationVoDecorator.GeohashMax);
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void MapCoordinates_PlusCode_has_correct_max_length_and_SetEmpty()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.PlusCode));
        prop.GetMaxLength().Should().Be(LocationVoDecorator.PlusCodeMax);
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetEmpty);
    }

    [Fact]
    public void MapCoordinates_AccuracyMeters_has_SetNull()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.AccuracyMeters));
        prop.GetMaxLength().Should().BeNull(because: "AccuracyMeters is numeric — no length");
        AnonRule(prop)!.Kind.Should().Be(AnonymizeKind.SetNull);
    }

    [Fact]
    public void MapCoordinates_HashId_has_correct_max_length_and_cleared_sentinel()
    {
        using var ctx = CoordinatesContext.Build();
        var prop = ComplexProp<CoordinatesEntity>(
            ctx.Model, "Coords", nameof(Coordinates.HashId));
        prop.GetMaxLength().Should().Be(sr_hashIdMax);
        var rule = AnonRule(prop);
        rule.Should().NotBeNull();
        rule.Kind.Should().Be(AnonymizeKind.Constant);
        rule.ConstantValue.Should().Be(sr_hashIdCleared);
    }

    [Fact]
    public void MapCoordinates_maps_as_complex_property_on_entity()
    {
        using var ctx = CoordinatesContext.Build();
        ctx.Model.FindEntityType(typeof(CoordinatesEntity))!
            .FindComplexProperty("Coords")
            .Should().NotBeNull("MapCoordinates should map Coordinates as a ComplexProperty");
    }

    [Fact]
    public void MapCoordinates_GeohashMax_and_PlusCodeMax_match_VO_encoder_caps()
    {
        // The decorator's named caps must match the Coordinates VO's encoder-intrinsic
        // geohash-10 / plus-code-13 lengths (single-source-of-truth guard against drift).
        var coords = Coordinates.Create(40.7128, -74.0060).Data!;
        LocationVoDecorator.GeohashMax.Should().Be(coords.Geohash.Length);
        LocationVoDecorator.PlusCodeMax.Should().Be(coords.PlusCode.Length);
    }

    // =========================================================================
    // Same-VO-type-twice column distinctness (EF Core 10 full-path uniquification)
    // =========================================================================

    [Fact]
    public void MapAdminLocation_same_CLR_type_twice_produces_distinct_column_sets()
    {
        using var ctx = TwoAdminContext.Build();
        var entityType = ctx.Model.FindEntityType(typeof(TwoAdminEntity))!;

        var homeLoc = entityType.FindComplexProperty("HomeLocation")!;
        var workLoc = entityType.FindComplexProperty("WorkLocation")!;

        var homeCity = homeLoc.ComplexType.FindProperty(nameof(AdminLocation.City))!;
        var workCity = workLoc.ComplexType.FindProperty(nameof(AdminLocation.City))!;

        var storeObject = StoreObjectIdentifier.Table(entityType.GetTableName()!);
        var homeCol = homeCity.GetColumnName(storeObject);
        var workCol = workCity.GetColumnName(storeObject);

        homeCol.Should().NotBe(
            workCol,
            because: "EF 10 full-path uniquification must produce distinct columns");
        homeCol.Should().Be("HomeLocation_City");
        workCol.Should().Be("WorkLocation_City");
        homeCity.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().NotBeNull();
        workCity.FindAnnotation(AnonymizationAnnotations.ANONYMIZE).Should().NotBeNull();
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private static IProperty ComplexProp<TEntity>(
        IModel model,
        string complexPropName,
        string memberName)
    {
        var entityType = model.FindEntityType(typeof(TEntity))!;
        var complexProp = entityType.FindComplexProperty(complexPropName)!;
        return complexProp.ComplexType.FindProperty(memberName)!;
    }

    private static AnonymizationRule? AnonRule(IProperty prop) =>
        prop.FindAnnotation(AnonymizationAnnotations.ANONYMIZE)?.Value as AnonymizationRule;

    // =========================================================================
    // Test entities
    // =========================================================================

    private sealed class StreetEntity
    {
        public int Id { get; set; }

        public StreetAddress Address { get; set; } = default!;
    }

    private sealed class AdminEntity
    {
        public int Id { get; set; }

        public AdminLocation Location { get; set; } = default!;
    }

    private sealed class CoordinatesEntity
    {
        public int Id { get; set; }

        public Coordinates Coords { get; set; } = default!;
    }

    private sealed class TwoAdminEntity
    {
        public int Id { get; set; }

        public AdminLocation HomeLocation { get; set; } = default!;

        public AdminLocation WorkLocation { get; set; } = default!;
    }

    // =========================================================================
    // DbContexts — model-build-only (connection never opened)
    // =========================================================================

    private sealed class StreetContext : DbContext
    {
        private StreetContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static StreetContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<StreetEntity>(b =>
                b.ComplexProperty(e => e.Address, cp => cp.MapStreetAddress()));
        }
    }

    private sealed class AdminContext : DbContext
    {
        private AdminContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static AdminContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<AdminEntity>(b =>
                b.ComplexProperty(e => e.Location, cp => cp.MapAdminLocation()));
        }
    }

    private sealed class CoordinatesContext : DbContext
    {
        private CoordinatesContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static CoordinatesContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<CoordinatesEntity>(b =>
                b.ComplexProperty(e => e.Coords, cp => cp.MapCoordinates()));
        }
    }

    private sealed class TwoAdminContext : DbContext
    {
        private TwoAdminContext(DbContextOptions options)
            : base(options)
        {
        }

        internal static TwoAdminContext Build() =>
            new(new DbContextOptionsBuilder()
                .UseNpgsql("Host=localhost")
                .Options);

        protected override void OnModelCreating(ModelBuilder mb)
        {
            mb.Entity<TwoAdminEntity>(b =>
            {
                b.ComplexProperty(e => e.HomeLocation, cp => cp.MapAdminLocation());
                b.ComplexProperty(e => e.WorkLocation, cp => cp.MapAdminLocation());
            });
        }
    }
}
