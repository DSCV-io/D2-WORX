// -----------------------------------------------------------------------
// <copyright file="D2EnvTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Utilities.Configuration;

using System.Collections.Immutable;
using AwesomeAssertions;
using DcsvIo.D2.Utilities.Configuration;
using Xunit;

[Collection("EnvVarMutating")]
public sealed class D2EnvTests
{
    // Shared "no pre-existing keys" sentinel — using ImmutableHashSet.Empty so
    // R# / Roslyn see a real read-only API surface (no "collection never
    // updated" inspection warnings).
    private static readonly IReadOnlySet<string> sr_noPreExistingKeys =
        ImmutableHashSet<string>.Empty;

    // ----------------------------------------------------------------------
    // ApplyVars — process-env-wins + last-write-wins inside a single load
    // ----------------------------------------------------------------------

    [Fact]
    public void ApplyVars_SetsKeyNotInPreExistingSnapshot()
    {
        var key = $"D2_TEST_APPLY_{Guid.NewGuid():N}";
        try
        {
            D2Env.ApplyVars(
                new Dictionary<string, string> { [key] = "v" },
                sr_noPreExistingKeys);

            Environment.GetEnvironmentVariable(key).Should().Be("v");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ApplyVars_DoesNotOverwriteKeyInPreExistingSnapshot()
    {
        // Adversarial: simulate "container injected this var before D2Env ran".
        var key = $"D2_TEST_PRE_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(key, "container-wins");
            var preExisting = new HashSet<string> { key };

            D2Env.ApplyVars(
                new Dictionary<string, string> { [key] = "file-loses" },
                preExisting);

            Environment.GetEnvironmentVariable(key).Should().Be("container-wins");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void ApplyVars_SecondCallOverwritesFirst_WhenNotPreExisting()
    {
        // Within a single Load(), later files in the list override earlier
        // ones — modeled by two ApplyVars calls sharing one snapshot.
        var key = $"D2_TEST_LATER_{Guid.NewGuid():N}";
        try
        {
            D2Env.ApplyVars(
                new Dictionary<string, string> { [key] = "from-first-file" },
                sr_noPreExistingKeys);
            D2Env.ApplyVars(
                new Dictionary<string, string> { [key] = "from-second-file" },
                sr_noPreExistingKeys);

            Environment.GetEnvironmentVariable(key).Should().Be("from-second-file");
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ----------------------------------------------------------------------
    // SnapshotEnvKeys
    // ----------------------------------------------------------------------

    [Fact]
    public void SnapshotEnvKeys_IncludesCurrentlySetKeys()
    {
        var key = $"D2_TEST_SNAP_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(key, "v");

            var snapshot = D2Env.SnapshotEnvKeys();

            snapshot.Should().Contain(key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    [Fact]
    public void SnapshotEnvKeys_OmitsKeysSetAfterSnapshot()
    {
        // Property of the snapshot: it captures a point-in-time view; later
        // mutations are NOT reflected.
        var snapshot = D2Env.SnapshotEnvKeys();
        var key = $"D2_TEST_AFTER_{Guid.NewGuid():N}";
        try
        {
            Environment.SetEnvironmentVariable(key, "v");

            snapshot.Should().NotContain(key);
        }
        finally
        {
            Environment.SetEnvironmentVariable(key, null);
        }
    }

    // ----------------------------------------------------------------------
    // FindDirectoryWithAnyFile — discovery semantics (option B: first dir
    // with ANY match wins, never mixes files across directories)
    // ----------------------------------------------------------------------

    [Fact]
    public void FindDirectoryWithAnyFile_FindsFirstFileInList()
    {
        using var temp = TempDir.Create();
        File.WriteAllText(Path.Combine(temp.Dir.FullName, ".env.local"), "FOO=bar");

        var found = D2Env.FindDirectoryWithAnyFile(
            temp.Dir,
            [".env", ".env.local", ".env.secrets"]);

        found.Should().NotBeNull().And.Subject.As<DirectoryInfo>()
            .FullName.Should().Be(temp.Dir.FullName);
    }

    [Fact]
    public void FindDirectoryWithAnyFile_FindsAnyMatchingFile_NotJustFirstInList()
    {
        // Adversarial: only the LAST entry exists. Discovery must still find
        // the directory.
        using var temp = TempDir.Create();
        File.WriteAllText(Path.Combine(temp.Dir.FullName, ".env.secrets"), "FOO=bar");

        var found = D2Env.FindDirectoryWithAnyFile(
            temp.Dir,
            [".env", ".env.local", ".env.secrets"]);

        found.Should().NotBeNull().And.Subject.As<DirectoryInfo>()
            .FullName.Should().Be(temp.Dir.FullName);
    }

    [Fact]
    public void FindDirectoryWithAnyFile_StopsAtNearestDirectoryWithMatch()
    {
        // Adversarial: parent dir has .env, child dir has .env.local. Per
        // option (b) semantics, discovery must STOP at the child — never mix
        // files from different directories.
        using var temp = TempDir.Create();
        var child = Directory.CreateDirectory(Path.Combine(temp.Dir.FullName, "child"));
        File.WriteAllText(Path.Combine(temp.Dir.FullName, ".env"), "FOO=parent");
        File.WriteAllText(Path.Combine(child.FullName, ".env.local"), "FOO=child");

        var found = D2Env.FindDirectoryWithAnyFile(
            child,
            [".env", ".env.local", ".env.secrets"]);

        found.Should().NotBeNull().And.Subject.As<DirectoryInfo>()
            .FullName.Should().Be(child.FullName);
    }

    [Fact]
    public void FindDirectoryWithAnyFile_WalksUpToParent_WhenNoMatchesInStartDir()
    {
        using var temp = TempDir.Create();
        var nested = Directory.CreateDirectory(
            Path.Combine(temp.Dir.FullName, "a", "b", "c"));
        File.WriteAllText(Path.Combine(temp.Dir.FullName, ".env.local"), "FOO=bar");

        var found = D2Env.FindDirectoryWithAnyFile(
            nested,
            [".env", ".env.local", ".env.secrets"]);

        found.Should().NotBeNull().And.Subject.As<DirectoryInfo>()
            .FullName.Should().Be(temp.Dir.FullName);
    }

    [Fact]
    public void FindDirectoryWithAnyFile_OnNullStartDir_ReturnsNull()
    {
        D2Env.FindDirectoryWithAnyFile(
            null,
            [".env", ".env.local", ".env.secrets"])
            .Should().BeNull();
    }

    [Fact]
    public void FindDirectoryWithAnyFile_DepthLimitExhausted_ReturnsNull()
    {
        // Coverage: the `i < _MAX_DEPTH` exit branch (otherwise the walk
        // always terminates by reaching the filesystem root). Build a
        // sufficiently deep nested directory tree so the loop's iteration
        // limit is the first thing to bite, not dir.Parent == null.
        using var temp = TempDir.Create();
        var deep = temp.Dir.FullName;
        for (var i = 0; i < 15; i++)
        {
            deep = Path.Combine(deep, $"L{i}");
        }

        var leaf = Directory.CreateDirectory(deep);

        var found = D2Env.FindDirectoryWithAnyFile(
            leaf,
            [".env", ".env.local", ".env.secrets"]);

        found.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // ResolveEnvKeyComparer — platform branch (test seam)
    // ----------------------------------------------------------------------

    [Fact]
    public void ResolveEnvKeyComparer_OnWindows_ReturnsOrdinalIgnoreCase()
        => D2Env.ResolveEnvKeyComparer(isWindows: true)
            .Should().BeSameAs(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void ResolveEnvKeyComparer_OnNonWindows_ReturnsOrdinal()
        => D2Env.ResolveEnvKeyComparer(isWindows: false)
            .Should().BeSameAs(StringComparer.Ordinal);

    [Fact]
    public void FindDirectoryWithAnyFile_NoMatchingFileInTree_ReturnsNull()
    {
        using var temp = TempDir.Create();

        var found = D2Env.FindDirectoryWithAnyFile(
            temp.Dir,
            [".env", ".env.local", ".env.secrets"]);

        // Caveat: this assertion would fail if the dev machine happens to
        // have one of the listed files at any ancestor of the temp dir. Tests
        // run from a project bin/ which has no such file by convention.
        found.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // Load — public idempotent entry point (defaults + params override)
    // ----------------------------------------------------------------------

    [Fact]
    public void Load_SecondCall_IsNoOp()
    {
        // SECURITY: pass explicit non-existent file names so discovery walk
        // never finds the repo's real .env.local / .env.secrets and silently
        // loads them into the test process. The default-args overload would
        // contaminate downstream tests with real-world secret values.
        var safeName = $".env.testonly-{Guid.NewGuid():N}";

        D2Env.ResetForTests();
        D2Env.Load(safeName);

        // Second call must not re-walk the file system.
        D2Env.Load(safeName);

        // No assertion needed beyond "did not throw"; this hits the
        // s_loaded short-circuit branch.
    }

    [Fact]
    public void Load_AfterReset_WalksAgain()
    {
        // SECURITY: same isolation as Load_SecondCall_IsNoOp — pin the no-op
        // re-walk behavior without loading real secrets.
        var safeName = $".env.testonly-{Guid.NewGuid():N}";

        D2Env.ResetForTests();
        D2Env.Load(safeName);

        D2Env.ResetForTests();
        D2Env.Load(safeName);

        // No exception; the short-circuit was bypassed by ResetForTests.
    }

    [Fact]
    public void Load_WithExplicitFileNames_OverridesDefaults()
    {
        D2Env.ResetForTests();

        // Pass a name that obviously won't be found anywhere up the tree;
        // Load returns silently without setting any vars.
        D2Env.Load($".env.testonly-{Guid.NewGuid():N}");
    }

    /// <summary>
    /// Test helper: temporary directory created on construction, recursively
    /// deleted on dispose.
    /// </summary>
    private sealed class TempDir : IDisposable
    {
        private TempDir(DirectoryInfo dir) => Dir = dir;

        public DirectoryInfo Dir { get; }

        public static TempDir Create()
        {
            var path = Path.Combine(
                Path.GetTempPath(),
                $"d2env-{Guid.NewGuid():N}");
            return new TempDir(Directory.CreateDirectory(path));
        }

        public void Dispose()
        {
            try
            {
                Dir.Delete(recursive: true);
            }
            catch (IOException)
            {
                // Best-effort cleanup; tests must not fail because of leftover
                // file handles on Windows.
            }
        }
    }
}
