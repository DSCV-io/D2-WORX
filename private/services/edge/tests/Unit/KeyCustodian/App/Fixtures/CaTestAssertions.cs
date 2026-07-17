// -----------------------------------------------------------------------
// <copyright file="CaTestAssertions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.App.Fixtures;

using System.Security.Cryptography.X509Certificates;
using AwesomeAssertions;

/// <summary>
/// Shared chain-validation assertions for CA-lifecycle tests: confirm a generated
/// leaf / intermediate certificate chains to a given root using a custom trust
/// store (no machine-store dependency).
/// </summary>
internal static class CaTestAssertions
{
    /// <summary>
    /// Asserts the supplied certificate DER chains to the supplied root DER under a
    /// custom-root trust store with revocation checking disabled.
    /// </summary>
    /// <param name="certificateDer">The leaf or intermediate certificate DER.</param>
    /// <param name="rootCertDer">The trust-anchor root certificate DER.</param>
    public static void AssertChainsToRoot(
        ReadOnlySpan<byte> certificateDer, byte[] rootCertDer)
    {
        using var certificate = X509CertificateLoader.LoadCertificate(certificateDer);
        using var root = X509CertificateLoader.LoadCertificate(rootCertDer);
        using var chain = new X509Chain();
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);

        // Verify at the certificate's issuance instant so a short-lived cert that has
        // expired relative to real wall-clock time still validates structurally.
        chain.ChainPolicy.VerificationTime = certificate.NotBefore.AddMinutes(1);

        chain.Build(certificate).Should().BeTrue(
            because: "the certificate must chain to the supplied root");
    }
}
