// -----------------------------------------------------------------------
// <copyright file="TestPaths.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.SourceGen;

/// <summary>
/// Locates dual-home contract specs + committed generated output at runtime
/// by walking up from the test bin directory until <c>D2.slnx</c> is
/// found. Source-of-truth specs drive the keycustodian parity / drift tests; we
/// read the live file rather than embedding a snapshot so drift surfaces
/// immediately.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// Returns the absolute path to the repo root (the directory containing
    /// <c>D2.slnx</c>), or throws if not found.
    /// </summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "D2.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root by walking up from " + AppContext.BaseDirectory);
    }

    /// <summary>Returns the absolute path to the keycustodian error-codes spec file.</summary>
    public static string KeyCustodianErrorCodesSpec() =>
        Path.Combine(
            RepoRoot(),
            "private",
            "contracts",
            "keycustodian-error-codes",
            "keycustodian-error-codes.spec.json");

    /// <summary>Returns the absolute path to the keycustodian error-codes schema file.</summary>
    public static string KeyCustodianErrorCodesSchema() =>
        Path.Combine(
            RepoRoot(),
            "private",
            "contracts",
            "keycustodian-error-codes",
            "schema.json");

    /// <summary>Returns the absolute path to the canonical error-codes schema file.</summary>
    public static string CanonicalErrorCodesSchema() =>
        Path.Combine(
            RepoRoot(),
            "public",
            "contracts",
            "error-codes",
            "error-codes.canonical.schema.json");

    /// <summary>
    /// Returns the absolute path to the private en-US.json translation-key source
    /// (product half of dual-values messages; includes keycustodian_* keys).
    /// </summary>
    public static string EnUsMessages() =>
        Path.Combine(
            RepoRoot(),
            "private",
            "contracts",
            "messages",
            "en-US.json");

    /// <summary>
    /// Returns the absolute path to the public en-US.json translation-key source
    /// (framework half — common_*, geo_*, contacts_*).
    /// </summary>
    public static string PublicEnUsMessages() =>
        Path.Combine(
            RepoRoot(),
            "public",
            "contracts",
            "messages",
            "en-US.json");

    /// <summary>Returns the absolute path to the error-category spec file.</summary>
    public static string ErrorCategorySpec() =>
        Path.Combine(
            RepoRoot(),
            "public",
            "contracts",
            "error-category",
            "error-category.spec.json");

    /// <summary>
    /// Returns the absolute path to the directory holding the committed
    /// keycustodian-error-codes generated files.
    /// </summary>
    public static string KeyCustodianGeneratedDir() =>
        Path.Combine(
            RepoRoot(),
            "private",
            "services",
            "edge",
            "key-custodian",
            "domain",
            "Generated",
            "DcsvIo.D2.Private.Edge.KeyCustodian.ErrorCodes.SourceGen",
            "DcsvIo.D2.Private.Edge.KeyCustodian.ErrorCodes.SourceGen.ErrorCodesGenerator");
}
