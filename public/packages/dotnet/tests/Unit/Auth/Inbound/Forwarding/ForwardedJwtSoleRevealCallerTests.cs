// -----------------------------------------------------------------------
// <copyright file="ForwardedJwtSoleRevealCallerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth.Inbound.Forwarding;

using System.IO;
using AwesomeAssertions;
using D2.Shared.Tests.Unit.Auth;
using Xunit;

/// <summary>
/// Pins the single-reveal-seam contract: <see cref="D2.Shared.Auth.Abstractions.ForwardedJwt.RevealForForwarding"/>
/// is the SOLE escape hatch for the raw bearer bytes, so its set of production
/// callers must be tightly controlled. A source-text scan over the
/// <c>server/</c> tree asserts that — apart from the type's own definition file
/// — NO production (non-test) source references <c>RevealForForwarding</c>.
/// </summary>
/// <remarks>
/// <para>
/// The holder is populated by the inbound auth surface and has exactly one
/// production READER: the outbound forwarding credential
/// (<c>ForwardedJwtCallCredentials</c>), the sole allowed caller in
/// <see cref="sr_allowedCallerFiles"/>. Any other production file referencing the
/// reveal symbol is a new, unreviewed reveal caller and fails this scan.
/// </para>
/// <para>
/// A compiled-IL scan (via a Cecil-style reader) would be stronger, but the
/// project takes no such dependency; a source-text scan is the strongest
/// automatable form available and is deterministic. It runs against the live
/// tree (resolved from the repo root), so a new caller surfaces immediately.
/// </para>
/// </remarks>
public sealed class ForwardedJwtSoleRevealCallerTests
{
    private const string _REVEAL_SYMBOL = "RevealForForwarding";

    // The type's own definition file legitimately contains the symbol (the
    // method declaration). Any other production file referencing it is a new
    // reveal caller and must be added here deliberately (with review).
    private static readonly string[] sr_definitionFiles =
    [
        "ForwardedJwt.cs",
    ];

    // Production files allowed to CALL the reveal seam. Exactly one: the outbound
    // forwarding credential, which reveals the held bearer bytes to attach them
    // as Authorization: Bearer on each outbound RPC. Any further addition is a
    // deliberate, reviewed widening of the reveal surface.
    private static readonly string[] sr_allowedCallerFiles =
    [
        "ForwardedJwtCallCredentials.cs",
    ];

    [Fact]
    public void RevealForForwarding_HasNoUnexpectedProductionCaller()
    {
        var scanRoots = new[]
        {
            Path.Combine(TestPaths.RepoRoot(), "public", "packages", "dotnet"),
            Path.Combine(TestPaths.RepoRoot(), "private", "services"),
            Path.Combine(TestPaths.RepoRoot(), "private", "packages", "dotnet"),
        };

        foreach (var root in scanRoots)
            Directory.Exists(root).Should().BeTrue($"production scan root missing: {root}");

        var offenders = new List<string>();
        foreach (var root in scanRoots)
        {
            foreach (var file in EnumerateProductionCsFiles(root))
            {
                var fileName = Path.GetFileName(file);
                if (sr_definitionFiles.Contains(fileName, StringComparer.Ordinal)
                    || sr_allowedCallerFiles.Contains(fileName, StringComparer.Ordinal))
                {
                    continue;
                }

                var text = File.ReadAllText(file);
                if (text.Contains(_REVEAL_SYMBOL, StringComparison.Ordinal))
                    offenders.Add(file);
            }
        }

        offenders.Should().BeEmpty(
            "RevealForForwarding is the single controlled reveal seam for the live "
            + "bearer credential; no unexpected production source may call it. "
            + "Add a deliberate, reviewed entry to the allowed-caller set if a new "
            + "legitimate consumer is introduced. Offenders: "
            + string.Join(", ", offenders));
    }

    private static IEnumerable<string> EnumerateProductionCsFiles(string scanRoot)
    {
        foreach (var file in Directory.EnumerateFiles(
            scanRoot, "*.cs", SearchOption.AllDirectories))
        {
            // Skip build output, generated code, and ALL test code (tests
            // legitimately reference the symbol to exercise it).
            if (IsExcludedPath(file))
                continue;

            yield return file;
        }
    }

    private static bool IsExcludedPath(string path)
    {
        var normalized = path.Replace('\\', '/');

        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/Generated/", StringComparison.Ordinal)
            || normalized.Contains(".g.cs", StringComparison.Ordinal)
            || normalized.Contains("/tests/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains(".Tests", StringComparison.OrdinalIgnoreCase);
    }
}
