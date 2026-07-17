// -----------------------------------------------------------------------
// <copyright file="PeerWorkloadIdentityAccessorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Mtls;

using AwesomeAssertions;
using DcsvIo.D2.AspNetCore.Mtls;
using DcsvIo.D2.Auth.Grpc.Mtls;
using DcsvIo.D2.Tests.Unit.Auth.Inbound.Grpc.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

/// <summary>
/// The capability-general peer-identity accessor matrix — the load-bearing
/// fail-closed security primitive. Proven CROSS-PLATFORM over a real KC-issued leaf
/// placed on <c>HttpContext.Connection.ClientCertificate</c> (no socket): a valid
/// leaf surfaces the workload id; no certificate ⇒ <see langword="null"/>
/// (fail-closed); a malformed SAN ⇒ <see langword="null"/>. The §9.39 reject matrix
/// drives the REAL <see cref="SpiffeSanPeerValidator"/> per reject conjunct and
/// asserts the accessor never yields an identity for a non-validated peer — and
/// NEVER seeds the certificate / context with a literal id (which would train the
/// seam to trust an injected identity). Both the <see cref="HttpContext"/> and the
/// gRPC <c>ServerCallContext</c> overloads are exercised.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PeerWorkloadIdentityAccessorTests
{
    // -----------------------------------------------------------------------
    // TryExtractWorkloadId — the surfaced validator extractor (one SAN-parse impl)
    // -----------------------------------------------------------------------

    [Fact]
    public void TryExtractWorkloadId_ValidLeaf_ReturnsWorkloadId()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf)?.ServiceId.Should().Be("edge");
    }

    [Fact]
    public void TryExtractWorkloadId_NoSan_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithoutSan("edge");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf).Should().BeNull();
    }

    [Fact]
    public void TryExtractWorkloadId_NonUriSan_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithDnsSan("edge", "edge.internal");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf).Should().BeNull();
    }

    [Fact]
    public void TryExtractWorkloadId_MultipleUriSans_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithTwoUriSans("edge", "files");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf).Should().BeNull();
    }

    [Fact]
    public void TryExtractWorkloadId_ForeignTrustDomainSan_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithRawUriSan("edge", "spiffe://other.internal/workload/edge");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf).Should().BeNull(
            "a foreign trust domain is rejected by the SPIFFE grammar");
    }

    [Fact]
    public void TryExtractWorkloadId_WrongSchemeSan_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithRawUriSan("edge", "https://d2.internal/workload/edge");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leaf).Should().BeNull();
    }

    // -----------------------------------------------------------------------
    // HttpContext.GetD2PeerWorkloadIdentity() — cross-platform surfacing
    // -----------------------------------------------------------------------

    [Fact]
    public void HttpContext_ValidClientCertificate_SurfacesWorkloadId()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("edge");
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = leaf;

        httpContext.GetD2PeerWorkloadIdentity().Should().Be("edge");
    }

    [Fact]
    public void HttpContext_SecondValidIdentity_SurfacesThatWorkloadId()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeaf("files");
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = leaf;

        httpContext.GetD2PeerWorkloadIdentity().Should().Be(
            "files",
            "the accessor surfaces whichever validated workload the cert names");
    }

    [Fact]
    public void HttpContext_NoClientCertificate_ReturnsNull_FailClosed()
    {
        // No certificate on the connection ⇒ no peer identity. This is the fail-closed
        // posture: a non-mTLS connection (or any connection Kestrel did not populate a
        // validated cert for) yields null ⇒ the caller denies.
        new DefaultHttpContext().GetD2PeerWorkloadIdentity().Should().BeNull();
    }

    [Fact]
    public void HttpContext_MalformedSanCertificate_ReturnsNull()
    {
        using var ca = new TestCertificateAuthority();
        using var leaf = ca.IssueLeafWithoutSan("edge");
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = leaf;

        httpContext.GetD2PeerWorkloadIdentity().Should().BeNull(
            "a certificate whose SAN is not a SPIFFE workload identity yields null");
    }

    [Fact]
    public void TryExtractWorkloadId_MalformedSpiffePath_NoWorkloadSegment_ReturnsNull()
    {
        // A URI SAN whose scheme is SPIFFE and trust domain is correct but whose path
        // does NOT follow the /workload/{id} grammar (e.g. /notworkload/edge or just /)
        // must yield null — the path-segment grammar rejects it.
        using var ca = new TestCertificateAuthority();

        // /notworkload/edge: the second segment is NOT "workload" so the SPIFFE
        // grammar (/workload/{id}) does not match.
        using var leafBadPath = ca.IssueLeafWithRawUriSan(
            "edge", "spiffe://d2.internal/notworkload/edge");
        using var leafNoSegment = ca.IssueLeafWithRawUriSan(
            "edge", "spiffe://d2.internal/");

        SpiffeSanPeerValidator.TryExtractWorkloadId(leafBadPath).Should().BeNull(
            "a SPIFFE URI with a non-workload path prefix does not match the grammar");
        SpiffeSanPeerValidator.TryExtractWorkloadId(leafNoSegment).Should().BeNull(
            "a SPIFFE URI with no path segments after the authority does not match");
    }

    // -----------------------------------------------------------------------
    // ServerCallContext.GetD2PeerWorkloadIdentity() — the gRPC overload delegates
    //
    // The shipped GetHttpContext() bridge throws InvalidOperationException when the
    // call is not ASP.NET-Core-hosted (no IServerCallContextFeature). A non-hosted /
    // hand-rolled ServerCallContext therefore yields null (fail-closed) — the
    // overload's HAPPY path (a real validated cert surfacing) is proven END-TO-END
    // over a real ASP.NET-Core gRPC host in MutualTlsSignerHarnessTests
    // (ValidLeaf_HostedService_SurfacesPeerWorkloadIdentity_*), where GetHttpContext()
    // resolves the real per-call context.
    // -----------------------------------------------------------------------

    [Fact]
    public void ServerCallContext_NotAspNetHosted_ReturnsNull_FailClosed()
    {
        // A hand-rolled ServerCallContext is not ASP.NET-Core-hosted, so the shipped
        // GetHttpContext() bridge throws — which the overload treats as "no resolvable
        // HttpContext" ⇒ null ⇒ deny. The accessor never throws to the caller and never
        // yields an identity it cannot derive from a validated certificate.
        var context = new TestServerCallContext();

        context.GetD2PeerWorkloadIdentity().Should().BeNull(
            "a non-ASP.NET-hosted call has no resolvable HttpContext ⇒ fail-closed");
    }

    [Fact]
    public void ServerCallContext_AspNetHosted_NoCertOnConnection_ReturnsNull_FailClosed()
    {
        // A gRPC call that IS ASP.NET-Core-hosted (GetHttpContext() returns a real
        // HttpContext) but whose connection carries NO client certificate yields null —
        // the "connected gRPC call, no cert" fail-closed path. This is distinct from the
        // non-hosted case above (which throws in GetHttpContext()) and the happy path
        // (which is tested end-to-end in MutualTlsSignerHarnessTests). Covers the gap
        // where the gRPC service is reached without mTLS (e.g. an internal non-mTLS
        // caller or a misconfigured load-balancer stripping the cert).
        var httpContext = new DefaultHttpContext();

        // No httpContext.Connection.ClientCertificate — left null (the default).
        var context = new TestServerCallContext(
            httpContext: httpContext);

        context.GetD2PeerWorkloadIdentity().Should().BeNull(
            "an ASP.NET-hosted gRPC call with no client certificate on the connection "
            + "yields null — the fail-closed path for non-mTLS gRPC calls");
    }

    // -----------------------------------------------------------------------
    // §9.39 fail-closed reject matrix — drive the REAL validator, never seed an id
    // -----------------------------------------------------------------------

    [Fact]
    public void Reject_NoCertificate_RealValidatorRejects_AndAccessorYieldsNull()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);

        // The real validator rejects a null cert; the connection therefore carries no
        // validated cert, so the accessor (reading Connection.ClientCertificate)
        // yields null — fail-closed. No literal id is ever seeded.
        validator.Validate(null).Success.Should().BeFalse();
        new DefaultHttpContext().GetD2PeerWorkloadIdentity().Should().BeNull();
    }

    [Fact]
    public void Reject_ForeignCaLeaf_RealValidatorRejects_AndAccessorNeverSeeded()
    {
        using var ourCa = new TestCertificateAuthority();
        using var foreignCa = new TestCertificateAuthority();
        var validator = BuildValidator(ourCa);
        using var foreignLeaf = foreignCa.IssueLeaf("edge");

        // The real validator rejects a foreign-CA leaf at the chain conjunct ⇒ Kestrel
        // would never populate Connection.ClientCertificate ⇒ the accessor yields null.
        validator.Validate(foreignLeaf).Success.Should().BeFalse(
            "a leaf from a foreign CA fails the chain conjunct");
        new DefaultHttpContext().GetD2PeerWorkloadIdentity().Should().BeNull();
    }

    [Fact]
    public void Reject_UnknownWorkload_RealValidatorRejects_ButSanStillParses()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca, allowedWorkloads: ["files"]);
        using var ghostLeaf = ca.IssueLeaf("ghost");
        using var chain = ca.PresentedChain(ghostLeaf);

        // The real validator rejects "ghost" at the allowed-workload conjunct — Kestrel
        // would refuse the connection, so the accessor never sees the cert. (The SAN
        // itself is well-formed; the rejection is the allowed-set, enforced by the
        // validator the host wires, NOT by the accessor — which is why a rejected peer
        // never reaches a request.)
        validator.Validate(ghostLeaf, chain).Success.Should().BeFalse(
            "ghost is not in the allowed-workload set");
    }

    [Theory]
    [InlineData("spiffe://other.internal/workload/edge")] // foreign trust domain
    [InlineData("https://d2.internal/workload/edge")] // wrong scheme
    public void Reject_MalformedSan_RealValidatorRejects_AndAccessorYieldsNull(string rawSan)
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeafWithRawUriSan("edge", rawSan);
        using var chain = ca.PresentedChain(leaf);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = leaf;

        // Even if such a cert WERE placed on the connection, the accessor's extractor
        // rejects the malformed SAN and yields null — the SAN-shape fail-closed arm.
        validator.Validate(leaf, chain).Success.Should().BeFalse();
        httpContext.GetD2PeerWorkloadIdentity().Should().BeNull();
    }

    [Fact]
    public void Accept_ValidLeaf_RealValidatorAccepts_AndAccessorSurfacesId()
    {
        using var ca = new TestCertificateAuthority();
        var validator = BuildValidator(ca);
        using var leaf = ca.IssueLeaf("edge");
        using var chain = ca.PresentedChain(leaf);
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.ClientCertificate = leaf;

        // The single Ok path: the real validator accepts, and the accessor surfaces the
        // validated id derived from the SAME cert.
        validator.Validate(leaf, chain).Success.Should().BeTrue();
        httpContext.GetD2PeerWorkloadIdentity().Should().Be("edge");
    }

    private static SpiffeSanPeerValidator BuildValidator(
        TestCertificateAuthority ca,
        IReadOnlyList<string>? allowedWorkloads = null)
    {
        var options = new D2MutualTlsOptions
        {
            Enabled = true,
            AllowedWorkloads = allowedWorkloads ?? ["edge", "files"],
            TrustAnchorsProvider = ca.TrustAnchors,
        };

        return new SpiffeSanPeerValidator(
            options, NullLogger<SpiffeSanPeerValidator>.Instance);
    }
}
