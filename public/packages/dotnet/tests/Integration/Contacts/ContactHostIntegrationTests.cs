// -----------------------------------------------------------------------
// <copyright file="ContactHostIntegrationTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.Contacts;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Contacts.EntityFrameworkCore;
using DcsvIo.D2.Contacts.ValueObjects;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Location.EntityFrameworkCore;
using DcsvIo.D2.Location.ValueObjects;
using DcsvIo.D2.Tests.Integration.DataGovernance;
using DcsvIo.D2.Validation.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Live-DB integration tests for the Contacts VO mapping and anonymization engine.
/// Shares the <see cref="PostgresFixture"/> container (one Postgres instance per xUnit
/// collection — see <c>PostgresCollectionDefinition</c>).
/// <para>
/// Scenarios covered:
/// <list type="bullet">
///   <item>Map round-trip: all VOs + value converters persist and reload cleanly.</item>
///   <item>Anonymization round-trip: every PII field → its exact tombstone; non-PII
///   retained; idempotent re-run.</item>
///   <item>Coordinates numeric coercion: Latitude/Longitude constant <c>"0"</c>
///   coerced to <c>0.0</c> on BOTH the Tier-B path (ContactHost) and the Tier-A
///   <c>ExecuteUpdateAsync</c> path (GeoOnlyHost), read back via BOTH EF materialization
///   and a raw SQL column query.</item>
///   <item>HashId overwritten: pre-erasure HashId replaced by the cleared sentinel
///   post-erasure.</item>
/// </list>
/// <c>CreateD2Index</c> is not tested here — its <c>CreateIndexOperation</c>
/// emission is unit-covered in <c>Unit/SharedEntityFrameworkCore/</c>, and live-DB
/// coverage (operation → Npgsql DDL → real Postgres index → <c>pg_indexes</c>
/// assertions) lives in
/// <c>Integration/SharedEntityFrameworkCore/CreateD2IndexIntegrationTests</c>.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class ContactHostIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private readonly List<ContactsHostDbContext> r_engineContexts = [];

    private ContactsHostDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of <see cref="ContactHostIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public ContactHostIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // GovDbContext.EnsureCreatedAsync creates the database (if it does not yet exist)
        // and the 0014 gov tables. If the database already exists, this is a no-op that
        // leaves existing tables untouched — the GovDbContext tables were already created
        // by whichever test in the collection ran first. This mirrors the
        // AnonymizationEngineGapTests pattern (creating the shared DB owner first ensures
        // the gov tables are always present regardless of xUnit collection run order).
        r_schemaCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        await using var govCtx = GovDbContext.Build(r_fixture.ConnectionString);
        await govCtx.Database.EnsureCreatedAsync();

        // ContactsHostDbContext tables are created via IF NOT EXISTS DDL so this call
        // is idempotent (safe whether the DB is fresh or the tables already exist from
        // a prior test run in the same collection).
        await ContactsHostDbContext.EnsureSchemaAsync(r_fixture.ConnectionString);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var ctx in r_engineContexts)
            await ctx.DisposeAsync();

        await r_schemaCtx.DisposeAsync();
        AnonymizationTierClassifier.ClearCache();
    }

    // =========================================================================
    // Map round-trip
    // =========================================================================

    [Fact]
    public async Task ContactHost_map_roundtrip_all_vo_members_and_converters_persist_and_reload()
    {
        var userId = Guid.NewGuid();
        var seeded = BuildContactHost(userId, includeOptionals: true);

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(seeded);
        await writeCtx.SaveChangesAsync();

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        // Personal
        row.Name.FirstName.Should().Be("Jane");
        row.Name.MiddleName.Should().Be("Q");
        row.Name.LastName.Should().Be("Public");
        row.Name.PreferredName.Should().Be("Janie");
        row.Name.HashId.Should().Be(seeded.Name.HashId);

        // NameAffixes round-trip (required all-nullable complex type)
        row.Affixes.Prefix.Should().Be(NamePrefix.Dr, "prefix round-trips through enum column");
        row.Affixes.PrefixCustom.Should().BeNull("only standard prefix seeded");
        row.Affixes.Suffix.Should().BeNull("no suffix seeded");
        row.Affixes.SuffixCustom.Should().BeNull("no suffix seeded");

        // Demographics round-trip (required all-nullable complex type)
        row.Demo.BiologicalSex.Should().Be(
            BiologicalSex.Female,
            "sex round-trips through nullable enum column");
        row.Demo.DateOfBirth.Should().BeNull("no date of birth seeded");

        // Professional — including Uri↔AbsoluteUri round-trip
        row.Work.CompanyName.Should().Be("Acme Inc");
        row.Work.JobTitle.Should().Be("Engineer");
        row.Work.Department.Should().Be("R&D");
        row.Work.CompanyWebsite.Should().NotBeNull();
        row.Work.CompanyWebsite!.AbsoluteUri.Should().Be("https://acme.example/");

        // StreetAddress
        row.Street.Line1.Should().Be("1 Fake St");
        row.Street.Line2.Should().Be("Suite 2");
        row.Street.Line3.Should().BeNull();
        row.Street.Line4.Should().BeNull();
        row.Street.Line5.Should().BeNull();
        row.Street.HashId.Should().Be(seeded.Street.HashId);

        // AdminLocation — CountryCode enum round-trip + SubdivisionCode struct round-trip
        row.Admin.CountryIso31661Alpha2Code.Should().Be(CountryCode.US);
        row.Admin.SubdivisionIso31662Code.Should().NotBeNull();
        row.Admin.SubdivisionIso31662Code!.Value.Value.Should().Be("US-NY");
        row.Admin.City.Should().Be("Springfield");
        row.Admin.PostalCode.Should().Be("00000");
        row.Admin.HashId.Should().Be(seeded.Admin.HashId);

        // Coordinates (all three representations + AccuracyMeters)
        row.Geo.Latitude.Should().BeApproximately(seeded.Geo.Latitude, 0.000001);
        row.Geo.Longitude.Should().BeApproximately(seeded.Geo.Longitude, 0.000001);
        row.Geo.Geohash.Should().Be(seeded.Geo.Geohash);
        row.Geo.PlusCode.Should().Be(seeded.Geo.PlusCode);
        row.Geo.AccuracyMeters.Should().BeApproximately(12.5, 0.001);
        row.Geo.HashId.Should().Be(seeded.Geo.HashId);

        // EmailAddress value converter (FromTrusted read side)
        row.Email.Should().NotBeNull();
        row.Email!.Value.Should().Be("jane@example.test");

        // PhoneNumber value converter (FromTrusted read side)
        row.Phone.Should().NotBeNull();
        row.Phone!.Value.Should().Be("15551230000");

        // Non-PII
        row.AccountTier.Should().Be("premium");
        row.IsAnonymized.Should().BeFalse();
    }

    [Fact]
    public async Task ContactHost_roundtrip_nullable_members_survive_as_null()
    {
        // Adversarial: row with minimal VOs (nulls where optional).
        var userId = Guid.NewGuid();
        var minimal = BuildContactHost(userId, includeOptionals: false);

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(minimal);
        await writeCtx.SaveChangesAsync();

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        row.Name.MiddleName.Should().BeNull();
        row.Name.LastName.Should().BeNull();
        row.Name.PreferredName.Should().BeNull();
        row.Work.JobTitle.Should().BeNull();
        row.Work.Department.Should().BeNull();
        row.Work.CompanyWebsite.Should().BeNull();
        row.Street.Line2.Should().BeNull();
        row.Geo.AccuracyMeters.Should().BeNull();
        row.Email.Should().BeNull();
        row.Phone.Should().BeNull();
    }

    [Fact]
    public async Task AdminLocation_empty_subdivision_reads_back_as_null()
    {
        // Adversarial: ""→null read-side SubdivisionCode converter.
        var userId = Guid.NewGuid();
        var adminNoSubdivision = AdminLocation.Create(
            countryIso31661Alpha2Code: CountryCode.DE,
            city: "Berlin").Data!;

        var host = new ContactsHostDbContext.ContactHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Personal.Create("Hans").Data!,
            Affixes = NameAffixes.Create(prefix: NamePrefix.Dr).Data!,
            Demo = Demographics.Create(biologicalSex: BiologicalSex.Female).Data!,
            Work = Professional.Create("Berlin Firm").Data!,
            Street = StreetAddress.Create("1 Test Str").Data!,
            Admin = adminNoSubdivision,
            Geo = Coordinates.Create(52.0, 13.0).Data!,
            AccountTier = "basic",
        };

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(host);
        await writeCtx.SaveChangesAsync();

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        row.Admin.SubdivisionIso31662Code.Should().BeNull(
            "empty subdivision string must read back as null");
        row.Admin.CountryIso31661Alpha2Code.Should().Be(CountryCode.DE);
    }

    // =========================================================================
    // LocalDate DOB round-trip (NodaTime LocalDate? → DATE Postgres column)
    // =========================================================================

    [Fact]
    public async Task Demographics_DateOfBirth_LocalDate_round_trips_through_date_column()
    {
        // Verifies that a non-null LocalDate? DateOfBirth seeded on Demographics
        // survives the LocalDate? → DATE Postgres column → LocalDate? EF round-trip.
        // This exercises the o.AddD2NodaTime() NodaTime value converter wired in
        // ContactsHostDbContext.Build().
        var userId = Guid.NewGuid();
        var dob = new NodaTime.LocalDate(1990, 3, 15);
        var demo = Demographics.Create(
            dateOfBirth: dob,
            biologicalSex: BiologicalSex.Female).Data!;

        var host = new ContactsHostDbContext.ContactHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = Personal.Create("Lee").Data!,
            Affixes = NameAffixes.Create(prefix: NamePrefix.Dr).Data!,
            Demo = demo,
            Work = Professional.Create("Test Corp").Data!,
            Street = StreetAddress.Create("1 Test St").Data!,
            Admin = AdminLocation.Create(countryIso31661Alpha2Code: CountryCode.US).Data!,
            Geo = Coordinates.Create(0.0, 0.0).Data!,
            AccountTier = "standard",
        };

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(host);
        await writeCtx.SaveChangesAsync();

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        row.Demo.DateOfBirth.Should().Be(
            new NodaTime.LocalDate(1990, 3, 15),
            "LocalDate? DateOfBirth must round-trip through the DATE Postgres column via AddD2NodaTime()");
        row.Demo.BiologicalSex.Should().Be(
            BiologicalSex.Female,
            "BiologicalSex must also survive the round-trip");
    }

    // =========================================================================
    // Anonymization round-trip (Tier B — ContactHost has Email Template)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_ContactHost_overwrites_pii_retains_non_pii()
    {
        var userId = Guid.NewGuid();
        var seeded = BuildContactHost(userId, includeOptionals: true);
        const string account_tier = "gold";
        seeded.AccountTier = account_tier;

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(seeded);
        await writeCtx.SaveChangesAsync();

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        // Personal tombstones
        row.Name.FirstName.Should().Be("Deleted");
        row.Name.MiddleName.Should().BeNull();
        row.Name.LastName.Should().BeNull();
        row.Name.PreferredName.Should().BeNull();
        row.Name.HashId.Should().Be(ContactVoDecorator.HashIdCleared);

        // NameAffixes tombstones (all fields → null via SetNull rules)
        row.Affixes.Prefix.Should().BeNull("NameAffixes.Prefix erasure → SetNull");
        row.Affixes.PrefixCustom.Should().BeNull("NameAffixes.PrefixCustom erasure → SetNull");
        row.Affixes.Suffix.Should().BeNull("NameAffixes.Suffix erasure → SetNull");
        row.Affixes.SuffixCustom.Should().BeNull("NameAffixes.SuffixCustom erasure → SetNull");

        // Demographics tombstones (all fields → null via SetNull rules)
        row.Demo.BiologicalSex.Should().BeNull("Demographics.BiologicalSex erasure → SetNull");
        row.Demo.DateOfBirth.Should().BeNull("Demographics.DateOfBirth erasure → SetNull");

        // Professional tombstones
        row.Work.CompanyName.Should().Be("Deleted");
        row.Work.JobTitle.Should().BeNull();
        row.Work.Department.Should().BeNull();
        row.Work.CompanyWebsite.Should().BeNull();

        // StreetAddress tombstones
        row.Street.Line1.Should().Be("[deleted]");
        row.Street.Line2.Should().BeNull();
        row.Street.HashId.Should().Be(LocationVoDecorator.HashIdCleared);

        // AdminLocation tombstones — Country KEPT (coarse-grained, no anonymize rule)
        row.Admin.City.Should().BeNull();
        row.Admin.PostalCode.Should().BeNull();
        row.Admin.SubdivisionIso31662Code.Should().BeNull();
        row.Admin.CountryIso31661Alpha2Code.Should().Be(CountryCode.US, "country is retained");
        row.Admin.HashId.Should().Be(LocationVoDecorator.HashIdCleared);

        // Email — Template rule, Tier B renders per-row
        var expectedEmail = $"deletedUser{userId:N}@deleted.user.dcsv.io";
        row.Email.Should().NotBeNull();
        row.Email!.Value.Should().Be(expectedEmail);

        // Phone — Constant rule
        row.Phone.Should().NotBeNull();
        row.Phone!.Value.Should().Be("10000000000");

        // Non-PII RETAINED
        row.AccountTier.Should().Be(account_tier);
        row.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeUserAsync_ContactHost_idempotent_rerun_skips_writes()
    {
        var userId = Guid.NewGuid();
        var seeded = BuildContactHost(userId, includeOptionals: false);

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(seeded);
        await writeCtx.SaveChangesAsync();

        var engine = BuildEngine();
        var first = await engine.AnonymizeUserAsync(userId);
        var second = await engine.AnonymizeUserAsync(userId);

        first.Success.Should().BeTrue();
        first.Data!.RowsAnonymized.Should().BeGreaterThan(0);
        second.Success.Should().BeTrue();
        second.Data!.RowsAnonymized.Should().Be(0);
        second.Data.AlreadyAnonymizedRows.Should().BeGreaterThan(0);

        // Values byte-stable on second run.
        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);
        row.Name.FirstName.Should().Be("Deleted");
        row.IsAnonymized.Should().BeTrue();
    }

    // =========================================================================
    // Coordinates numeric coercion (Tier-B path via ContactHost)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_ContactHost_coordinates_latitude_longitude_land_zero()
    {
        var userId = Guid.NewGuid();
        var seeded = BuildContactHost(userId, includeOptionals: false);

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(seeded);
        await writeCtx.SaveChangesAsync();

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);
        result.Success.Should().BeTrue();

        // EF materialization read path.
        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        row.Geo.Latitude.Should().Be(0.0, "constant \"0\" coerced to double 0.0 (Tier-B path)");
        row.Geo.Longitude.Should().Be(0.0, "constant \"0\" coerced to double 0.0 (Tier-B path)");
        row.Geo.Geohash.Should().BeEmpty("SetEmpty tombstone");
        row.Geo.PlusCode.Should().BeEmpty("SetEmpty tombstone");
        row.Geo.AccuracyMeters.Should().BeNull("SetNull on nullable double");
        row.Geo.HashId.Should().Be(LocationVoDecorator.HashIdCleared);

        // Raw SQL read — confirms the DB column itself holds 0, not a CLR artifact.
        var id = seeded.Id;
        var rawLat = await readCtx.Database
            .SqlQuery<double>(
                $"SELECT \"Geo_Latitude\" AS \"Value\" FROM \"ContactHosts\" WHERE \"Id\" = {id}")
            .FirstAsync();

        rawLat.Should().Be(
            0.0,
            "raw Postgres column must hold 0.0 — the live proof that Convert.ChangeType"
            + "(\"0\", typeof(double)) lands correctly on the Tier-B SetPropertyValue path");
    }

    // =========================================================================
    // Coordinates numeric coercion (Tier-A path via GeoOnlyHost)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_GeoOnlyHost_TierA_ExecuteUpdate_lat_lon_land_zero()
    {
        var userId = Guid.NewGuid();
        var coords = Coordinates.Create(48.8566, 2.3522, 5.0).Data!;

        var host = new ContactsHostDbContext.GeoOnlyHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Geo = coords,
        };

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.GeoOnlyHosts.Add(host);
        await writeCtx.SaveChangesAsync();

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);
        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        // EF materialization read path.
        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.GeoOnlyHosts.FirstAsync(h => h.UserId == userId);

        // ===================================================================
        // Tier-A ExecuteUpdateAsync path: Convert.ChangeType("0", typeof(double)) → 0.0d.
        // LIVE confirmation of the numeric-Constant coercion design.
        row.Geo.Latitude.Should().Be(
            0.0,
            "LIVE PROOF — Tier-A ExecuteUpdateAsync: constant \"0\" coerced to double 0.0");
        row.Geo.Longitude.Should().Be(
            0.0,
            "LIVE PROOF — Tier-A ExecuteUpdateAsync: constant \"0\" coerced to double 0.0");
        row.Geo.Geohash.Should().BeEmpty();
        row.Geo.PlusCode.Should().BeEmpty();
        row.Geo.AccuracyMeters.Should().BeNull();
        row.Geo.HashId.Should().Be(LocationVoDecorator.HashIdCleared);
        row.IsAnonymized.Should().BeTrue();

        // Raw SQL read — belt-and-braces: confirms the DB column itself holds 0.
        var id = host.Id;
        var rawLat = await readCtx.Database
            .SqlQuery<double>(
                $"SELECT \"Geo_Latitude\" AS \"Value\" FROM \"GeoOnlyHosts\" WHERE \"Id\" = {id}")
            .FirstAsync();

        rawLat.Should().Be(
            0.0,
            "raw Postgres column must hold 0.0 — DB-level proof of the Tier-A coercion");
    }

    // =========================================================================
    // HashId overwritten (dedicated focused fact)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_HashId_overwritten_with_cleared_sentinel()
    {
        var userId = Guid.NewGuid();
        var seeded = BuildContactHost(userId, includeOptionals: false);

        // Capture pre-erasure HashIds (all non-sentinel values).
        var preNameHashId = seeded.Name.HashId;
        var preStreetHashId = seeded.Street.HashId;
        var preAdminHashId = seeded.Admin.HashId;
        var preGeoHashId = seeded.Geo.HashId;

        preNameHashId.Should().NotBe(ContactVoDecorator.HashIdCleared);
        preStreetHashId.Should().NotBe(LocationVoDecorator.HashIdCleared);
        preAdminHashId.Should().NotBe(LocationVoDecorator.HashIdCleared);
        preGeoHashId.Should().NotBe(LocationVoDecorator.HashIdCleared);

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.ContactHosts.Add(seeded);
        await writeCtx.SaveChangesAsync();

        var engine = BuildEngine();
        await engine.AnonymizeUserAsync(userId);

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.ContactHosts.FirstAsync(h => h.UserId == userId);

        row.Name.HashId.Should().Be(ContactVoDecorator.HashIdCleared);
        row.Street.HashId.Should().Be(LocationVoDecorator.HashIdCleared);
        row.Admin.HashId.Should().Be(LocationVoDecorator.HashIdCleared);
        row.Geo.HashId.Should().Be(LocationVoDecorator.HashIdCleared);
    }

    // =========================================================================
    // Seed helpers + engine builder
    // =========================================================================

    private static ContactsHostDbContext.ContactHost BuildContactHost(
        Guid userId,
        bool includeOptionals)
    {
        var name = includeOptionals
            ? Personal.Create("Jane", "Q", "Public", "Janie").Data!
            : Personal.Create("Jane").Data!;

        // NameAffixes: required complex type (all-nullable VO; at least one member seeded).
        var affixes = NameAffixes.Create(prefix: NamePrefix.Dr).Data!;

        // Demographics: required complex type (all-nullable VO; at least one member seeded).
        var demo = Demographics.Create(biologicalSex: BiologicalSex.Female).Data!;

        var work = includeOptionals
            ? Professional.Create("Acme Inc", "Engineer", "R&D", "https://acme.example").Data!
            : Professional.Create("Acme Inc").Data!;

        var street = includeOptionals
            ? StreetAddress.Create("1 Fake St", "Suite 2").Data!
            : StreetAddress.Create("1 Fake St").Data!;

        var admin = AdminLocation.Create(
            countryIso31661Alpha2Code: CountryCode.US,
            subdivisionIso31662Code: SubdivisionCode.FromString("US-NY"),
            city: "Springfield",
            postalCode: "00000").Data!;

        var geo = includeOptionals
            ? Coordinates.Create(40.0, -70.0, 12.5).Data!
            : Coordinates.Create(40.0, -70.0).Data!;

        var host = new ContactsHostDbContext.ContactHost
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Affixes = affixes,
            Demo = demo,
            Work = work,
            Street = street,
            Admin = admin,
            Geo = geo,
            AccountTier = "premium",
        };

        if (includeOptionals)
        {
            host.Email = EmailAddress.Create("jane@example.test").Data;
            host.Phone = PhoneNumber.Create("15551230000").Data;
        }

        return host;
    }

    private AnonymizationEngine BuildEngine(int batchSize = 500)
    {
        var opts = Options.Create(new AnonymizationEngineOptions { BatchSize = batchSize });
        var ctx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        r_engineContexts.Add(ctx);
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }
}
