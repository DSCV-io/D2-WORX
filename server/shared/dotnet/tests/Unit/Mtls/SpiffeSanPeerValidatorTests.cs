// -----------------------------------------------------------------------
// <copyright file="SpiffeSanPeerValidatorTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Mtls;

using System.Net;
using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;
using D2.Shared.AspNetCore.Mtls;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// The adversarial peer-validator matrix — the heart of the mTLS server side and
/// the fail-open-footgun guard. Mints real certificates with a self-contained test
/// CA and asserts the default-deny three-conjunct check: a valid leaf is accepted;
/// a wrong trust domain, an unknown workload, a foreign-CA chain, an expired leaf,
/// a missing / non-URI / multiple-URI SAN, a CA-as-leaf, garbage bytes, and a null
/// certificate are ALL rejected — and the validator NEVER throws.
/// </summary>
public sealed class SpiffeSanPeerValidatorTests
{
    // -----------------------------------------------------------------------
    // Accept — all three conjuncts hold
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ValidLeaf_TrustDomainOk_WorkloadAllowed_Accepts()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeaf("edge");
        using var chain = ca.PresentedChain(leaf);

        var result = validator.Validate(leaf, chain);

        result.Success.Should().BeTrue();
        result.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public void Validate_ValidLeaf_SecondAllowedWorkload_Accepts()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca, allowedWorkloads: ["edge", "files"]);
        using var leaf = ca.IssueLeaf("files");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Reject — conjunct 2: SPIFFE SAN trust domain / shape
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_WrongTrustDomainSan_Rejects()
    {
        // The leaf chains to our CA but its SAN names a foreign trust domain.
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithRawUriSan(
            "edge", "spiffe://other.internal/workload/edge");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_NoSpiffeSan_CnOnly_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithoutSan("edge");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_NonUriSan_DnsOnly_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithDnsSan("edge", "edge.internal");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_MultipleUriSans_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithTwoUriSans("edge", "files");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_WrongSchemeSan_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithRawUriSan(
            "edge", "https://d2.internal/workload/edge");
        using var chain = ca.PresentedChain(leaf);

        validator.Validate(leaf, chain).Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Reject — conjunct 3: workload membership
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_UnknownWorkload_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca, allowedWorkloads: ["files"]);
        using var leaf = ca.IssueLeaf("edge"); // edge not in the allowed set
        using var chain = ca.PresentedChain(leaf);

        var result = validator.Validate(leaf, chain);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Reject — conjunct 1: chain to the configured internal CA
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_LeafChainsToForeignCa_Rejects()
    {
        // A leaf from a DIFFERENT CA — valid SPIFFE SAN, allowed workload, but it
        // does NOT chain to our configured trust anchor.
        using var ourCa = new TestCertificateAuthority();
        using var foreignCa = new TestCertificateAuthority();
        var validator = BuildValidator(ourCa);
        using var foreignLeaf = foreignCa.IssueLeaf("edge");

        validator.Validate(foreignLeaf).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_NoTrustAnchorsConfigured_Rejects()
    {
        // Defense: even a leaf from our CA is rejected when no anchors resolve —
        // the validator never accepts on an empty anchor set.
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca, trustAnchorsProvider: () => []);
        using var leaf = ca.IssueLeaf("edge");

        validator.Validate(leaf).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_ExpiredLeaf_Rejects()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var expired = ca.IssueExpiredLeaf("edge");
        using var chain = ca.PresentedChain(expired);

        validator.Validate(expired, chain).Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Defense-in-depth + fail-closed
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_CaPresentedAsLeaf_Rejects()
    {
        // A CA cert (BasicConstraints CA=true) presented as a client leaf — even
        // with a valid SAN + allowed workload, a CA must not pass as a leaf. The
        // intermediate's pathLength=0 + the explicit not-a-CA guard both reject it.
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var caAsLeaf = ca.IssueCaAsLeaf("edge");
        using var chain = ca.PresentedChain(caAsLeaf);

        validator.Validate(caAsLeaf, chain).Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_NullCertificate_Rejects_NoThrow()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);

        // The validator never throws by design — a direct call that returns a
        // rejection (rather than escaping an exception) IS the no-throw assertion.
        var result = validator.Validate(null);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_GarbageCertificate_Rejects_NoThrow()
    {
        // A self-signed cert with no SAN and no chain to our CA — stands in for a
        // structurally-valid-but-untrusted "garbage" presentation. The validator
        // must reject without throwing.
        using var ca = new TestCertificateAuthority();
        using var foreign = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var rootAsLeaf = X509CertificateLoader.LoadCertificate(
            foreign.RootCertificate.RawData);

        // The validator never throws by design — a direct call returning a rejection
        // (rather than escaping an exception) IS the no-throw assertion.
        var result = validator.Validate(rootAsLeaf);

        result.Success.Should().BeFalse();
    }

    [Fact]
    public void Validate_MachineStoreValidButNotOurAnchor_Rejects()
    {
        // The explicit fail-open guard: a leaf that chains to a foreign CA is
        // rejected even though SslPolicyErrors are never consulted — the validator
        // rebuilds the chain against OUR anchors, not the machine store.
        using var ourCa = new TestCertificateAuthority();
        using var foreignCa = new TestCertificateAuthority();
        var validator = BuildValidator(ourCa);
        using var foreignLeaf = foreignCa.IssueLeaf("edge");

        // Even if a (hypothetical) machine-store validation passed, our validator
        // rejects because the leaf does not chain to the configured anchor.
        validator.Validate(foreignLeaf).Success.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    // Additive-not-skip invariant: the verdict is a PEER verdict only.
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ReturnsOnlyAPeerVerdict_NeverATokenVerdict()
    {
        // A valid leaf yields Ok — but Ok here asserts ONLY peer-workload trust; it
        // says nothing about a forwarded token (which is validated independently at
        // the auth layer). The validator surface carries no token concept at all.
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeaf("edge");
        using var chain = ca.PresentedChain(leaf);

        var result = validator.Validate(leaf, chain);

        result.Success.Should().BeTrue();

        // A rejected peer is Forbidden/Unauthorized — a transport-peer verdict, not
        // a token-validation outcome.
        using var foreignCa = new TestCertificateAuthority();
        using var foreignLeaf = foreignCa.IssueLeaf("edge");
        using var foreignChain = foreignCa.PresentedChain(foreignLeaf);
        validator.Validate(foreignLeaf, foreignChain).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    // -----------------------------------------------------------------------
    // Trust-domain enforcement — grammar-level (not a configurable option)
    // -----------------------------------------------------------------------

    [Fact]
    public void Validate_ForeignTrustDomainSan_Rejects()
    {
        // The trust domain is FIXED at d2.internal by the SPIFFE grammar — it is NOT
        // a configurable option on D2MutualTlsOptions (M-1 regression pin). A leaf
        // whose SAN names a foreign trust domain (e.g. spiffe://prod.internal/…)
        // chains to our CA but is rejected because SpiffeWorkloadIdentity.Parse
        // hard-rejects any non-d2.internal host. The reject is grammar-enforced, not
        // via a separate configurable gate.
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithRawUriSan(
            "edge", "spiffe://prod.internal/workload/edge");
        using var chain = ca.PresentedChain(leaf);

        var result = validator.Validate(leaf, chain);

        result.Success.Should().BeFalse(
            "a leaf with a foreign trust domain SAN is rejected by the SPIFFE grammar");
    }

    private static SpiffeSanPeerValidator BuildValidator(
        TestCertificateAuthority ca,
        IReadOnlyList<string>? allowedWorkloads = null,
        Func<X509Certificate2Collection>? trustAnchorsProvider = null)
    {
        var options = new D2MutualTlsOptions
        {
            Enabled = true,
            AllowedWorkloads = allowedWorkloads ?? ["edge", "files"],
            TrustAnchorsProvider = trustAnchorsProvider ?? ca.TrustAnchors,
        };

        return new SpiffeSanPeerValidator(
            options, NullLogger<SpiffeSanPeerValidator>.Instance);
    }
}
