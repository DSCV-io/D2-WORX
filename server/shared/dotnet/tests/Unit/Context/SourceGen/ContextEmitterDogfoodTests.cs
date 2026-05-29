// -----------------------------------------------------------------------
// <copyright file="ContextEmitterDogfoodTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Context.SourceGen;

using System;
using System.IO;
using System.Linq;
using AwesomeAssertions;
using Xunit;

/// <summary>
/// Regression test pinning the dogfood discipline for the context source
/// generator. The shared <c>StringExt.Falsey</c> / <c>Truthy</c> polyfill
/// (wired via the <c>Compile Include</c> from <c>source-gen-shared/</c>)
/// must be the only path the emitter / generator code uses to test for
/// null/empty/whitespace strings — never a direct call to
/// <c>string.IsNullOrEmpty</c> or <c>string.IsNullOrWhiteSpace</c>. A future
/// edit re-introducing such a call is a regression of the bug class that
/// shipped before the shared polyfill existed.
/// </summary>
public sealed class ContextEmitterDogfoodTests
{
    [Fact]
    public void ContextSourceGenSources_ContainNoIsNullOrEmptyOrIsNullOrWhiteSpace()
    {
        var sourceDir = LocateSourceGenDirectory();
        sourceDir.Should().NotBeNull(
            "the context/source-gen directory should be discoverable from the test bin path");

        var sources = Directory.GetFiles(sourceDir, "*.cs", SearchOption.AllDirectories);
        sources.Should().NotBeEmpty("context/source-gen has source files");

        var offenders = sources
            .Select(path => new
            {
                Path = path,
                Content = File.ReadAllText(path),
            })
            .Where(f =>
                f.Content.Contains("string.IsNullOrEmpty", StringComparison.Ordinal) ||
                f.Content.Contains("string.IsNullOrWhiteSpace", StringComparison.Ordinal))
            .Select(f => Path.GetFileName(f.Path))
            .ToArray();

        offenders.Should().BeEmpty(
            "context/source-gen production code must dogfood the shared "
            + "StringExt.Falsey / StringExt.Truthy polyfill from source-gen-shared/. "
            + "Direct calls to string.IsNullOrEmpty / string.IsNullOrWhiteSpace are "
            + "forbidden — the polyfill exists, so use it. Offending files: "
            + string.Join(", ", offenders));
    }

    private static string? LocateSourceGenDirectory()
    {
        // tests bin path: server/shared/dotnet/tests/bin/Debug/net10.0/D2.Shared.Tests.dll
        // walk up to repo root, then back down to context/source-gen.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName, "server", "shared", "dotnet", "context", "source-gen");
            if (Directory.Exists(candidate))
                return candidate;

            dir = dir.Parent;
        }

        return null;
    }
}
