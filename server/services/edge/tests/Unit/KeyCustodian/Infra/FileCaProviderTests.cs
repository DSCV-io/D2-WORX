// -----------------------------------------------------------------------
// <copyright file="FileCaProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.Infra;

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.KeyCustodian.Infra.Configuration;
using D2.Edge.KeyCustodian.Infra.Vault.File;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NodaTime;
using IClock = D2.Shared.Time.IClock;
using TestClock = D2.Shared.Time.TestClock;

/// <summary>
/// Adversarial tests for <see cref="FileCaProvider"/>: a valid PEM chain loads +
/// chain-validates + caches; a missing file / malformed PEM / non-chaining
/// intermediate / expired cert each fails loud; key/cert bytes never appear in any
/// log entry. Every test generates its OWN throwaway PEM in a temp directory — it
/// NEVER reads the deny-ruled <c>secrets/</c> tree.
/// </summary>
public sealed class FileCaProviderTests : IDisposable
{
    private readonly string r_dir;
    private readonly Instant r_baseInstant = Instant.FromUtc(2026, 1, 1, 0, 0);

    public FileCaProviderTests()
    {
        r_dir = Path.Combine(Path.GetTempPath(), "kc-ca-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(r_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(r_dir))
            Directory.Delete(r_dir, recursive: true);
    }

    [Fact]
    public void GetSeedCaMaterial_ValidChain_LoadsAndChainValidates()
    {
        WriteValidChain();

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeTrue();
        result.Data!.RootCertificateDer.Should().NotBeEmpty();
        result.Data!.RootPrivateKeyPkcs8.Should().NotBeEmpty();
        result.Data!.IntermediateCertificateDer.Should().NotBeEmpty();
        result.Data!.IntermediatePrivateKeyPkcs8.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSeedCaMaterial_CalledTwice_ReturnsFreshMaterialEachTime()
    {
        // Provider no longer caches the private-key bytes — each call loads fresh
        // so a caller that calls Zero() on the first result cannot poison a
        // subsequent call.
        WriteValidChain();
        var provider = BuildProvider();

        var first = provider.GetSeedCaMaterial();
        var second = provider.GetSeedCaMaterial();

        first.Success.Should().BeTrue();
        second.Success.Should().BeTrue();

        // Different instances — zeroing the first does not affect the second.
        first.Data.Should().NotBeSameAs(
            second.Data,
            because: "single-use contract: each call returns a fresh LoadedCaMaterial");
    }

    // Regression test for B2-F3: after zeroing the first result, a second call must
    // still return valid (non-zeroed) material — proving the cache was not poisoned.
    [Fact]
    public void GetSeedCaMaterial_AfterZero_SecondCallReturnsUnzeroedMaterial()
    {
        WriteValidChain();
        var provider = BuildProvider();

        var first = provider.GetSeedCaMaterial();
        first.Data!.Zero(); // simulate what the seeder does after wrapping

        var second = provider.GetSeedCaMaterial();

        second.Success.Should().BeTrue();
        second.Data!.RootPrivateKeyPkcs8.Should()
            .Contain(b => b != 0, because: "fresh load after Zero() must return un-zeroed private key bytes");
    }

    [Theory]
    [InlineData(CaCertificateFiles.ROOT_CERT_FILE_NAME)]
    [InlineData(CaCertificateFiles.ROOT_KEY_FILE_NAME)]
    [InlineData(CaCertificateFiles.INTERMEDIATE_CERT_FILE_NAME)]
    [InlineData(CaCertificateFiles.INTERMEDIATE_KEY_FILE_NAME)]
    public void GetSeedCaMaterial_MissingFile_ReturnsTypedFailure(string missingFile)
    {
        // B1-F1/B2-F1 regression: provider returns D2Result failure, never throws.
        WriteValidChain();
        System.IO.File.Delete(Path.Combine(r_dir, missingFile));

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse(
            because: "a missing CA file must return a typed failure, not throw");
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_MalformedCertPem_ReturnsTypedFailure()
    {
        // B1-F1/B2-F1 regression: malformed PEM returns a typed failure, not a throw.
        WriteValidChain();
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_CERT_FILE_NAME), "not a pem");

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_MalformedKeyPem_ReturnsTypedFailure()
    {
        // B1-F1/B2-F1 regression: malformed key PEM returns a typed failure, not a throw.
        WriteValidChain();
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_KEY_FILE_NAME),
            "-----BEGIN PRIVATE KEY-----\nZ\n-----END PRIVATE KEY-----");

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_IntermediateDoesNotChainToRoot_ReturnsTypedFailure()
    {
        // A valid root + a valid intermediate that was signed by a DIFFERENT root.
        var (rootCertPem, rootKeyPem, _, _) = GenerateChainPem(r_baseInstant);
        var (_, _, otherIntCertPem, otherIntKeyPem) = GenerateChainPem(r_baseInstant);

        System.IO.File.WriteAllText(Path.Combine(r_dir, CaCertificateFiles.ROOT_CERT_FILE_NAME), rootCertPem);
        System.IO.File.WriteAllText(Path.Combine(r_dir, CaCertificateFiles.ROOT_KEY_FILE_NAME), rootKeyPem);
        System.IO.File.WriteAllText(Path.Combine(r_dir, CaCertificateFiles.INTERMEDIATE_CERT_FILE_NAME), otherIntCertPem);
        System.IO.File.WriteAllText(Path.Combine(r_dir, CaCertificateFiles.INTERMEDIATE_KEY_FILE_NAME), otherIntKeyPem);

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse(
            because: "an intermediate that does not chain to the supplied root must return a typed failure");
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_ExpiredCert_ReturnsTypedFailure()
    {
        WriteValidChain();

        // A clock far past the 10y root / 1y intermediate validity window.
        var futureClock = new TestClock(r_baseInstant + Duration.FromDays(4000));

        var result = BuildProvider(futureClock).GetSeedCaMaterial();

        result.Success.Should().BeFalse(
            because: "a certificate outside its validity window must return a typed failure");
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_EmptyFile_ReturnsTypedFailure()
    {
        WriteValidChain();
        System.IO.File.WriteAllText(Path.Combine(r_dir, CaCertificateFiles.ROOT_CERT_FILE_NAME), string.Empty);

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_OversizedFile_ReturnsTypedFailure()
    {
        // B2-F5 regression: a file exceeding the 4 KB cap must return a typed failure.
        WriteValidChain();
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_CERT_FILE_NAME),
            new string('A', 4097));

        var result = BuildProvider().GetSeedCaMaterial();

        result.Success.Should().BeFalse(because: "files over the 4 KB cap must return a typed failure");
        result.ErrorCode.Should().Be("KEYCUSTODIAN_NO_ACTIVE_ISSUING_CA");
    }

    [Fact]
    public void GetSeedCaMaterial_FailurePaths_NeverLogKeyOrCertBytes()
    {
        WriteValidChain();
        var keyPem = System.IO.File.ReadAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_KEY_FILE_NAME));
        var keyFirstBase64Line = FirstBase64Line(keyPem);

        // Corrupt the chain so the load fails (and logs a sanitized reason).
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.INTERMEDIATE_CERT_FILE_NAME), "not a pem");
        var logger = new CapturingLogger<FileCaProvider>();

        // No throw expected — provider returns typed failure.
        BuildProvider(logger: logger).GetSeedCaMaterial();

        var allMessages = string.Join("\n", logger.Entries.Select(e => e.Message));
        allMessages.Should().NotContain("BEGIN PRIVATE KEY");
        allMessages.Should().NotContain("BEGIN CERTIFICATE");
        allMessages.Should().NotContain(
            keyFirstBase64Line, because: "no key body must appear in any log entry");
    }

    private static (string RootCertPem, string RootKeyPem, string IntCertPem, string IntKeyPem)
        GenerateChainPem(Instant created)
    {
        var clock = new TestClock(created);

        var root = CaCertificateGeneration.GenerateRootCa(
            "D2 Test Root CA", Duration.FromDays(3650), clock).Data!;

        string rootCertPem;
        string rootKeyPem;
        string intCertPem;
        string intKeyPem;

        using (var rootKeyEc = ECDsa.Create())
        {
            rootKeyEc.ImportPkcs8PrivateKey(root.PrivateKeyPkcs8, out _);

            using var rootCert = X509CertificateLoader.LoadCertificate(root.CertificateDer);

            var intermediate = CaCertificateGeneration.GenerateIntermediateCa(
                "D2 Test Issuing CA", rootCert, rootKeyEc, Duration.FromDays(365), clock).Data!;

            rootCertPem = ToCertPem(root.CertificateDer);
            rootKeyPem = ToKeyPem(root.PrivateKeyPkcs8);
            intCertPem = ToCertPem(intermediate.CertificateDer);
            intKeyPem = ToKeyPem(intermediate.PrivateKeyPkcs8);

            intermediate.Zero();
        }

        root.Zero();
        return (rootCertPem, rootKeyPem, intCertPem, intKeyPem);
    }

    private static string ToCertPem(byte[] der) =>
        new(PemEncoding.Write("CERTIFICATE", der));

    private static string ToKeyPem(byte[] pkcs8) =>
        new(PemEncoding.Write("PRIVATE KEY", pkcs8));

    private static string FirstBase64Line(string pem)
    {
        foreach (var line in pem.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Length > 0 && !trimmed.StartsWith("-----", StringComparison.Ordinal))
                return trimmed;
        }

        return "no-base64-body";
    }

    private void WriteValidChain()
    {
        var (rootCertPem, rootKeyPem, intCertPem, intKeyPem) = GenerateChainPem(r_baseInstant);
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_CERT_FILE_NAME), rootCertPem);
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.ROOT_KEY_FILE_NAME), rootKeyPem);
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.INTERMEDIATE_CERT_FILE_NAME), intCertPem);
        System.IO.File.WriteAllText(
            Path.Combine(r_dir, CaCertificateFiles.INTERMEDIATE_KEY_FILE_NAME), intKeyPem);
    }

    private FileCaProvider BuildProvider(
        IClock? clock = null, ILogger<FileCaProvider>? logger = null)
    {
        var options = Options.Create(new KeyCustodianInfraOptions
        {
            RootKeyPath = r_dir,
            ConnectionString = "Host=localhost;Port=1;Database=keycustodian_db;Username=u;Password=p",
        });
        return new FileCaProvider(
            options,
            logger ?? NullLogger<FileCaProvider>.Instance,
            clock ?? new TestClock(r_baseInstant + Duration.FromHours(1)));
    }

    /// <summary>Thread-safe capturing logger that records formatted messages.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Enqueue((logLevel, formatter(state, exception)));
    }
}
