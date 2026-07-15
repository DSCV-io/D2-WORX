// -----------------------------------------------------------------------
// <copyright file="FileRootKeyProviderTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.Infra;

using System.Security.Cryptography;
using System.Text;
using DcsvIo.D2.Encryption;
using DcsvIo.D2.Private.Edge.KeyCustodian.App.Infrastructure.Vault;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Configuration;
using DcsvIo.D2.Private.Edge.KeyCustodian.Infra.Vault.File;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

/// <summary>
/// Adversarial tests for <see cref="FileRootKeyProvider"/> over the BOTH-FILE
/// matrix: a required primary (<c>root.key</c>) and an optional successor
/// (<c>root-next.key</c>). Every test generates its OWN throwaway hex files in a
/// temp directory — it NEVER reads the deny-ruled <c>secrets/</c> tree.
/// </summary>
public sealed class FileRootKeyProviderTests : IDisposable
{
    private readonly string r_dir;

    public FileRootKeyProviderTests()
    {
        r_dir = Path.Combine(Path.GetTempPath(), "kc-root-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(r_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(r_dir))
            Directory.Delete(r_dir, recursive: true);
    }

    // =========================================================================
    // Primary root.key — happy paths.
    // =========================================================================

    [Fact]
    public void GetRootKeyring_PrimaryOnly_BuildsSingleKidKeyring_PrimaryIsActiveKid()
    {
        WritePrimary(ValidHex());

        var keyring = BuildProvider().GetRootKeyring();

        keyring.ActiveKid.Should().Be(RootKeyKids.PRIMARY_KID);
        keyring.AllKids.Should().BeEquivalentTo([RootKeyKids.PRIMARY_KID]);
    }

    // Trailing variants: bare (no whitespace), LF, CRLF, spaces.
    [Theory]
    [InlineData("")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("   ")]
    public void GetRootKeyring_PrimaryWithTrailingWhitespace_TrimsAndLoads(string trailer)
    {
        WritePrimary(ValidHex() + trailer);

        var keyring = BuildProvider().GetRootKeyring();

        keyring.ActiveKid.Should().Be(RootKeyKids.PRIMARY_KID);
    }

    [Fact]
    public void GetRootKeyring_AadContext_IsUtf8OfRootServiceKey()
    {
        WritePrimary(ValidHex());

        var keyring = BuildProvider().GetRootKeyring();

        keyring.AadContext.ToArray()
            .Should().Equal(Encoding.UTF8.GetBytes(KeyCustodianRootKey.ROOT_SERVICE_KEY));
    }

    // =========================================================================
    // Primary root.key — adversarial fail-fast paths.
    // =========================================================================

    [Fact]
    public void GetRootKeyring_PrimaryMissing_Throws()
    {
        // No file written.
        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    public void GetRootKeyring_PrimaryEmptyOrWhitespace_Throws(string content)
    {
        WritePrimary(content);

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>();
    }

    // non-hex chars / odd-length hex / hex-shaped-but-invalid-digits.
    [Theory]
    [InlineData("nothex!!")]
    [InlineData("abc")]
    [InlineData("zzzzzzzzzzzzzzzzzzzzzzzz")]
    public void GetRootKeyring_PrimaryNotValidHex_Throws(string content)
    {
        WritePrimary(content);

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>().WithMessage("*hex*");
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void GetRootKeyring_PrimaryWrongByteLength_Throws(int byteLength)
    {
        WritePrimary(HexOf(byteLength));

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>().WithMessage("*decoded to*");
    }

    [Fact]
    public void GetRootKeyring_PrimaryRawBinaryNonUtf8_Throws()
    {
        // A raw-binary file (not hex text) — ReadAllText mangles non-UTF8 bytes,
        // and the result is not valid hex of the right length.
        System.IO.File.WriteAllBytes(
            PrimaryPath(), RandomNumberGenerator.GetBytes(40));

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>();
    }

    // =========================================================================
    // Successor root-next.key — absent / present / corrupt.
    // =========================================================================

    [Fact]
    public void GetRootKeyring_SuccessorAbsent_SingleKidKeyring()
    {
        WritePrimary(ValidHex());

        // No successor file written.
        var keyring = BuildProvider().GetRootKeyring();

        keyring.AllKids.Should().NotContain(RootKeyKids.NEXT_KID);
        keyring.AllKids.Should().BeEquivalentTo([RootKeyKids.PRIMARY_KID]);
    }

    [Fact]
    public void GetRootKeyring_SuccessorPresent_TwoKidKeyring_PrimaryStillActive()
    {
        WritePrimary(ValidHex());
        WriteSuccessor(ValidHex());

        var keyring = BuildProvider().GetRootKeyring();

        keyring.ActiveKid.Should().Be(RootKeyKids.PRIMARY_KID);
        keyring.AllKids.Should()
            .BeEquivalentTo([RootKeyKids.PRIMARY_KID, RootKeyKids.NEXT_KID]);
    }

    [Fact]
    public void GetRootKeyring_SuccessorPresent_SuccessorKidIsDecryptable()
    {
        WritePrimary(ValidHex());
        WriteSuccessor(ValidHex());

        var keyring = BuildProvider().GetRootKeyring();

        keyring.TryGetKey(RootKeyKids.NEXT_KID, out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t ")]
    [InlineData("nothex!!")]
    [InlineData("abc")]
    public void GetRootKeyring_SuccessorPresentButCorrupt_FailsFast(string content)
    {
        WritePrimary(ValidHex());
        WriteSuccessor(content);

        var act = () => BuildProvider().GetRootKeyring();

        // Present-but-bad successor is an operator error mid-rotation, not "absent".
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(33)]
    [InlineData(64)]
    public void GetRootKeyring_SuccessorWrongByteLength_FailsFast(int byteLength)
    {
        WritePrimary(ValidHex());
        WriteSuccessor(HexOf(byteLength));

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>().WithMessage("*decoded to*");
    }

    [Fact]
    public void GetRootKeyring_SuccessorRawBinaryNonUtf8_Throws()
    {
        WritePrimary(ValidHex());

        // A raw-binary successor file — ReadAllText mangles non-UTF8 bytes,
        // and the result is not valid hex of the right length.
        System.IO.File.WriteAllBytes(
            SuccessorPath(), RandomNumberGenerator.GetBytes(40));

        var act = () => BuildProvider().GetRootKeyring();

        act.Should().Throw<InvalidOperationException>();
    }

    // =========================================================================
    // Caching.
    // =========================================================================

    [Fact]
    public void GetRootKeyring_CalledTwice_ReturnsSameCachedInstance()
    {
        WritePrimary(ValidHex());
        var provider = BuildProvider();

        var first = provider.GetRootKeyring();
        var second = provider.GetRootKeyring();

        second.Should().BeSameAs(first);
    }

    [Fact]
    public void GetRootKeyring_Cached_SurvivesFileDeletionAfterFirstRead()
    {
        WritePrimary(ValidHex());
        var provider = BuildProvider();
        var first = provider.GetRootKeyring();

        // Delete the file — the cached keyring is unaffected (one disk read).
        System.IO.File.Delete(PrimaryPath());

        provider.GetRootKeyring().Should().BeSameAs(first);
    }

    // =========================================================================
    // Helpers — all key bytes are random throwaway material in a temp dir.
    // =========================================================================

    private static string ValidHex() => HexOf(PayloadCryptoKeyring.KEY_SIZE_BYTES);

    private static string HexOf(int byteLength) =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(byteLength));

    private string PrimaryPath() => Path.Combine(r_dir, RootKeyKids.PRIMARY_FILE_NAME);

    private string SuccessorPath() => Path.Combine(r_dir, RootKeyKids.NEXT_FILE_NAME);

    private void WritePrimary(string content) =>
        System.IO.File.WriteAllText(PrimaryPath(), content);

    private void WriteSuccessor(string content) =>
        System.IO.File.WriteAllText(SuccessorPath(), content);

    private FileRootKeyProvider BuildProvider()
    {
        var options = Options.Create(new KeyCustodianInfraOptions { RootKeyPath = r_dir });
        return new FileRootKeyProvider(options, NullLogger<FileRootKeyProvider>.Instance);
    }
}
