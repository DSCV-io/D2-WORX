// -----------------------------------------------------------------------
// <copyright file="InvoiceAnonymizationIntegrationTests.cs" company="DCSV">
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Live-DB integration tests for Invoice partial anonymization and
/// unique-template per-row resolution. Uses the shared Postgres container.
/// <para>
/// Invoice: maps <c>AdminLocation</c> twice (billing + shipping) — proves independent
/// tombstoning, no cross-column leak, and financial-scalar retention (Tier A).
/// </para>
/// <para>
/// UniqueContactRow: with a <c>{UserId}</c> unique-template on the email
/// column — proves that erasing two rows with DISTINCT owners produces two DISTINCT
/// tombstone values, so no unique-constraint violation fires.
/// </para>
/// </summary>
[Collection("Postgres")]
[Trait("Category", "Integration")]
public sealed class InvoiceAnonymizationIntegrationTests : IAsyncLifetime
{
    private readonly PostgresFixture r_fixture;
    private readonly List<ContactsHostDbContext> r_engineContexts = [];

    private ContactsHostDbContext r_schemaCtx = null!;

    /// <summary>
    /// Initializes a new instance of <see cref="InvoiceAnonymizationIntegrationTests"/>.
    /// </summary>
    /// <param name="fixture">The shared Postgres Testcontainers fixture.</param>
    public InvoiceAnonymizationIntegrationTests(PostgresFixture fixture)
    {
        r_fixture = fixture;
    }

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        // Same GovDbContext-first ordering as ContactHostIntegrationTests and
        // AnonymizationEngineGapTests — ensures the shared database + gov tables exist
        // before the Contacts schema is layered on top via IF NOT EXISTS DDL.
        r_schemaCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        await using var govCtx = GovDbContext.Build(r_fixture.ConnectionString);
        await govCtx.Database.EnsureCreatedAsync();
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
    // Invoice: partial anonymization + same-VO-twice no-cross-leak (Tier A)
    // =========================================================================

    [Fact]
    public async Task AnonymizeUserAsync_Invoice_TierA_clears_pii_retains_financials()
    {
        var userId = Guid.NewGuid();

        var billing = AdminLocation.Create(
            countryIso31661Alpha2Code: CountryCode.US,
            city: "Alpha",
            postalCode: "11111").Data!;

        var shipping = AdminLocation.Create(
            countryIso31661Alpha2Code: CountryCode.GB,
            city: "Beta",
            postalCode: "22222").Data!;

        var customer = Personal.Create("Alice", lastName: "Smith").Data!;

        var invoice = new ContactsHostDbContext.Invoice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            BillingAddress = billing,
            ShippingAddress = shipping,
            CustomerName = customer,
            TotalAmount = 199.99m,
            TaxAmount = 16.00m,
            InvoiceNumber = "INV-001",
        };

        await using var writeCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        writeCtx.Invoices.Add(invoice);
        await writeCtx.SaveChangesAsync();

        // Pre-erasure assertion: proves the same-VO-twice columns are genuinely
        // distinct, not tautological. A test that seeds identical values would not prove
        // the column-naming uniquification is real.
        await using var preCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var pre = await preCtx.Invoices.FirstAsync(i => i.UserId == userId);

        pre.BillingAddress.City.Should().Be("Alpha", "billing city pre-erasure");
        pre.ShippingAddress.City.Should().Be("Beta", "shipping city pre-erasure — distinct");

        // Erasure (Tier A — no Template columns → ExecuteUpdateAsync path)
        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(userId);

        result.Success.Should().BeTrue();
        result.Data!.RowsAnonymized.Should().Be(1);

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var row = await readCtx.Invoices.FirstAsync(i => i.UserId == userId);

        // PII tombstoned independently — no cross-leak
        row.BillingAddress.City.Should().BeNull("billing city tombstoned");
        row.BillingAddress.PostalCode.Should().BeNull("billing postal tombstoned");
        row.BillingAddress.SubdivisionIso31662Code.Should().BeNull();
        row.BillingAddress.CountryIso31661Alpha2Code.Should().Be(
            CountryCode.US,
            "billing country kept — coarse-grained");
        row.BillingAddress.HashId.Should().Be(LocationVoDecorator.HashIdCleared);

        row.ShippingAddress.City.Should().BeNull("shipping city tombstoned");
        row.ShippingAddress.PostalCode.Should().BeNull("shipping postal tombstoned");
        row.ShippingAddress.SubdivisionIso31662Code.Should().BeNull();
        row.ShippingAddress.CountryIso31661Alpha2Code.Should().Be(
            CountryCode.GB,
            "shipping country kept — coarse-grained");
        row.ShippingAddress.HashId.Should().Be(LocationVoDecorator.HashIdCleared);

        // CustomerName tombstoned
        row.CustomerName.FirstName.Should().Be("Deleted");
        row.CustomerName.LastName.Should().BeNull();
        row.CustomerName.HashId.Should().Be(ContactVoDecorator.HashIdCleared);

        // Financials RETAINED
        row.TotalAmount.Should().Be(199.99m);
        row.TaxAmount.Should().Be(16.00m);
        row.InvoiceNumber.Should().Be("INV-001");
        row.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeUserAsync_Invoice_same_vo_twice_columns_are_distinct_at_model_level()
    {
        // Column-distinctness pin: asserts that BillingAddress.City and
        // ShippingAddress.City map to DISTINCT DB columns, not aliased to the same one.
        // EF Core names complex properties with dot notation (e.g. "BillingAddress.City")
        // as the IProperty.Name; the DB column name uses underscores ("BillingAddress_City").
        await using var ctx = ContactsHostDbContext.Build(r_fixture.ConnectionString);

        var entityType = ctx.Model.FindEntityType(typeof(ContactsHostDbContext.Invoice))!;

        // Find City via the BillingAddress and ShippingAddress complex property builders.
        var billingComplex = entityType.FindComplexProperty("BillingAddress");
        var shippingComplex = entityType.FindComplexProperty("ShippingAddress");

        billingComplex.Should().NotBeNull("BillingAddress complex property must exist");
        shippingComplex.Should().NotBeNull("ShippingAddress complex property must exist");

        var billingCityProp = billingComplex.ComplexType.FindProperty("City");
        var shippingCityProp = shippingComplex.ComplexType.FindProperty("City");

        billingCityProp.Should().NotBeNull("BillingAddress.City property must exist");
        shippingCityProp.Should().NotBeNull("ShippingAddress.City property must exist");

        // Use the table StoreObjectIdentifier to get the fully qualified column name,
        // which includes the complex-property-path prefix (e.g. "BillingAddress_City").
        var storeObj = Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(
            entityType.GetTableName()!);

        var billingCol = billingCityProp.GetColumnName(storeObj);
        var shippingCol = shippingCityProp.GetColumnName(storeObj);
        billingCol.Should().NotBe(
            shippingCol,
            "billing and shipping city must map to distinct DB columns");
    }

    // =========================================================================
    // UniqueContactRow: .Unique(template) per-row resolution
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public async Task AnonymizeUserAsync_UniqueContactRow_distinct_owners_no_unique_constraint_violation()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await SeedUniqueRow(userA, "a@example.test");
        await SeedUniqueRow(userB, "b@example.test");

        var engine = BuildEngine();
        var resultA = await engine.AnonymizeUserAsync(userA);
        var resultB = await engine.AnonymizeUserAsync(userB);

        // Neither call must throw DbUpdateException (unique-constraint violation).
        resultA.Success.Should().BeTrue();
        resultB.Success.Should().BeTrue();

        await using var readCtx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        var rowA = await readCtx.UniqueContactRows.FirstAsync(r => r.UserId == userA);
        var rowB = await readCtx.UniqueContactRows.FirstAsync(r => r.UserId == userB);

        var expectedA = $"deletedUser{userA:N}@deleted.user.dcsv.io";
        var expectedB = $"deletedUser{userB:N}@deleted.user.dcsv.io";

        rowA.Email!.Value.Should().Be(expectedA, "per-row UserId template rendered for A");
        rowB.Email!.Value.Should().Be(expectedB, "per-row UserId template rendered for B");
        rowA.Email.Value.Should().NotBe(
            rowB.Email.Value,
            "two distinct owners produce two distinct tombstones");

        rowA.IsAnonymized.Should().BeTrue();
        rowB.IsAnonymized.Should().BeTrue();
    }

    [Fact]
    public async Task AnonymizeUserAsync_UniqueContactRow_same_owner_two_rows_collision_boundary()
    {
        // Adversarial negative case: two rows share the same UserId. The {UserId}-only
        // template resolves to the SAME value for both rows, causing a unique-constraint
        // violation on the second SaveChanges (Tier-B materialize-mutate path). This
        // pins the documented caller-responsibility boundary: the template author
        // must include a per-ROW token if multiple rows can share an owner.
        var sharedUser = Guid.NewGuid();
        await SeedUniqueRow(sharedUser, "first@example.test");
        await SeedUniqueRow(sharedUser, "second@example.test");

        var engine = BuildEngine();
        var result = await engine.AnonymizeUserAsync(sharedUser);

        // The engine returns a failure (not a silent half-erase) — boundary pinned.
        result.Success.Should().BeFalse(
            "same-owner UserId-only template collides on the unique constraint; "
            + "engine must not silently succeed — caller-responsibility boundary: "
            + "the template must include a per-row token if multiple rows share an owner");
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    private async Task SeedUniqueRow(Guid userId, string email)
    {
        await using var ctx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        ctx.UniqueContactRows.Add(new ContactsHostDbContext.UniqueContactRow
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Email = EmailAddress.Create(email).Data,
        });
        await ctx.SaveChangesAsync();
    }

    private AnonymizationEngine BuildEngine(int batchSize = 500)
    {
        var opts = Options.Create(new AnonymizationEngineOptions { BatchSize = batchSize });
        var ctx = ContactsHostDbContext.Build(r_fixture.ConnectionString);
        r_engineContexts.Add(ctx);
        return new AnonymizationEngine(ctx, opts, NullLogger<AnonymizationEngine>.Instance);
    }
}
