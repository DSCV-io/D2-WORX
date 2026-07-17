// -----------------------------------------------------------------------
// <copyright file="SealedDomainRemovalTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App;

using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Private.Auth;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Queries.GetKeyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Client.Keyring;
using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Errors;
using DcsvIo.D2.Private.Edge.KeyCustodian.Domain.ValueObjects;
using DcsvIo.D2.Private.Encryption;
using Microsoft.Extensions.Options;
using Xunit;

/// <summary>
/// Pins that the sealed domains (audit / notifications / courier) are REMOVED from the KC
/// symmetric payload catalog while the domain-generic symmetric machinery is PRESERVED
/// (exercised via the internal fixture-domain seam). Every assertion that references a REAL
/// sealed value lives in THIS one class so it runs sequentially (xUnit serializes test methods
/// within a class) â€” the fixture seam is a static registry, so the two-bypass test that
/// re-admits <c>"audit"</c> must never run in parallel with an "audit"-rejects pin.
/// </summary>
public sealed class SealedDomainRemovalTests
{
    private static readonly string[] sr_sealedValues =
        [ProductEncryptionDomains.AUDIT, ProductEncryptionDomains.NOTIFICATIONS, ProductEncryptionDomains.COURIER];

    // ---- Fixture-seam properties -------------------------------------------------------------

    [Fact]
    public void FixtureSeam_DefaultEmpty_CreateRejectsUnregisteredFixtureValue()
    {
        // Production behavior when unused: a fixture value is NOT a catalog member. Uses a
        // value NO other test registers, so the assertion is deterministic under parallelism.
        KeyDomain.Create("payload-fixture-unregistered-sentinel").Success.Should().BeFalse();
    }

    [Fact]
    public void FixtureSeam_Registered_ResolvesAesPayload_ThenGoneAfterDispose()
    {
        // A per-test-unique value so a concurrent test sharing PAYLOAD_A can't keep the
        // reference count above zero and flip the post-dispose "gone" assertion.
        var value = "payload-fixture-scoped-" + Guid.NewGuid().ToString("N");
        var registration = KeyDomain.RegisterFixturePayloadDomainForTesting(value);

        var created = KeyDomain.Create(value);
        created.Success.Should().BeTrue();
        created.Data!.KeyType.Should().Be(KeyType.AesPayload);
        KeyDomain.FromTrusted(value).KeyType.Should().Be(KeyType.AesPayload);

        registration.Dispose();

        KeyDomain.Create(value).Success.Should()
            .BeFalse("disposing the scope unregisters the fixture domain");
    }

    // ---- Sealed-value removal pins (real values â€” sequential within this class) ---------------

    [Fact]
    public void SealedValues_AbsentFromCatalog()
    {
        foreach (var sealedValue in sr_sealedValues)
            KeyDomain.All.Should().NotContain(d => d.Value == sealedValue);
    }

    [Fact]
    public void SealedValue_Create_RejectsUnknownKeyDomain()
    {
        foreach (var sealedValue in sr_sealedValues)
        {
            var result = KeyDomain.Create(sealedValue);
            result.Success.Should().BeFalse();
            result.ErrorCode.Should().Be(KeyCustodianErrorCodes.KEYCUSTODIAN_UNKNOWN_KEY_DOMAIN);
        }
    }

    [Fact]
    public void SealedValue_FromTrusted_ThrowsCorruptRowFailLoud()
    {
        foreach (var sealedValue in sr_sealedValues)
        {
            var act = () => KeyDomain.FromTrusted(sealedValue);
            act.Should().Throw<ArgumentException>(
                "a de-cataloged sealed domain in a stored row is corrupt â†’ fail loud");
        }
    }

    // ---- Boot validator sealed-mode arm ------------------------------------------------------

    [Fact]
    public void Validator_SealedGrant_RefusedWithPreciseMessage()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit-consumer"] = [ProductEncryptionDomains.AUDIT];

        var violation = options.Validate();

        violation.Should().NotBeNull();
        violation.Should().Contain("sealed-mode", "the operator gets the precise sealed-mode reason");
    }

    [Fact]
    public void Validator_NonCatalogGarbageGrant_RefusedWithGenericMessage()
    {
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["some-workload"] = ["not-a-real-domain"];

        var violation = options.Validate();

        violation.Should().NotBeNull();
        violation.Should().Contain("non-catalog", "generic non-catalog error for plain garbage");
    }

    [Fact]
    public void Validator_FixtureDomainGrant_IsValid()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["fixture-consumer"] =
            [FixturePayloadDomains.PAYLOAD_A];

        options.Validate().Should().BeNull("a symmetric fixture payload domain is grantable");
    }

    // ---- GetKeyring sealed-mode sharp-400 (the TWO-BYPASS test) ----------------------------

    [Fact]
    public async Task GetKeyring_ReAdmittedSealedDomain_FiresSealedArm_PastKeyTypeFork()
    {
        // BYPASS 1 (nonexistence): re-admit the REAL "audit" value through the fixture seam,
        // simulating the guarded-against regression (a sealed domain back in the catalog with
        // its spec-derived AesPayload binding). Now Create("audit") passes AND the existing
        // step-3 KeyType fork passes (it binds AesPayload) â€” proving the sealed arm is GENUINELY
        // ADDITIVE past that fork.
        using var fixtureSeam = FixturePayloadDomains.Register(ProductEncryptionDomains.AUDIT);
        KeyDomain.Create(ProductEncryptionDomains.AUDIT).Data!.KeyType.Should().Be(
            KeyType.AesPayload, "bypass 1: the re-admitted sealed domain is AesPayload-bound");

        // BYPASS 2 (validator): construct the allowed-set DIRECTLY (never running Validate()),
        // so step-2 authority passes for a grant the boot validator would have refused.
        var options = new KeyringDomainAuthorityOptions();
        options.AllowedKeyringDomainsByWorkload["audit-consumer"] = [ProductEncryptionDomains.AUDIT];
        var policy = new OptionsKeyringDomainAuthorityPolicy(Options.Create(options));

        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        var context = KcAppTestKit.ContextWithOriginAndCaller<GetKeyringHandler>(
            RequestOrigin.CrossProcessHop,
            immediateCaller: "audit-consumer",
            scopes: new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring });
        var handler =
            new GetKeyringHandler(context, db, KcAppTestKit.BuildTestRootCrypto(), policy);

        var result = await handler.HandleAsync(new GetKeyringInput(ProductEncryptionDomains.AUDIT));

        // The NEW sealed-mode arm fires with the sharp-400 â€” the DB is never queried.
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(
            KeyCustodianErrorCodes.KEYCUSTODIAN_KEY_TYPE_DOMAIN_MISMATCH);
    }

    // ---- Preserved-machinery proof: getKeyring serves a symmetric FIXTURE domain --------------

    [Fact]
    public async Task GetKeyring_FixtureSymmetricDomain_ServesKeyringEndToEnd()
    {
        using var fixtureSeam = FixturePayloadDomains.Register();
        var rootCrypto = KcAppTestKit.BuildTestRootCrypto();
        var options = KcAppTestKit.BuildOptions();

        await using var db = KeyCustodianTestDbContext.CreateEmpty();
        await KcAppTestKit.SeedKeyAsync(
            db,
            rootCrypto,
            options,
            FixturePayloadDomains.PAYLOAD_A,
            KeyType.AesPayload,
            KeyStatus.Active,
            KcAppTestKit.SR_BaseInstant);

        var authority = new KeyringDomainAuthorityOptions();
        authority.AllowedKeyringDomainsByWorkload["fixture-consumer"] =
            [FixturePayloadDomains.PAYLOAD_A];
        var policy = new OptionsKeyringDomainAuthorityPolicy(Options.Create(authority));

        var context = KcAppTestKit.ContextWithOriginAndCaller<GetKeyringHandler>(
            RequestOrigin.CrossProcessHop,
            immediateCaller: "fixture-consumer",
            scopes: new HashSet<string>(StringComparer.Ordinal) { ProductScopes.Internal.Kc.Keyring });
        var handler = new GetKeyringHandler(context, db, rootCrypto, policy);

        var result =
            await handler.HandleAsync(new GetKeyringInput(FixturePayloadDomains.PAYLOAD_A));

        // The preserved domain-generic symmetric machinery serves the fixture domain: a keyring
        // with the active kid + decryptable entries + the domain AAD.
        result.Success.Should().BeTrue();
        result.Data!.Entries.Should().NotBeEmpty();
        result.Data.ActiveKid.Should().NotBeNullOrEmpty();
    }
}
