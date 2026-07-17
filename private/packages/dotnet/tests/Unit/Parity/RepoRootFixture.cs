// -----------------------------------------------------------------------
// <copyright file="RepoRootFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Parity;

using System;
using System.IO;

/// <summary>
/// Sentinel-based monorepo root resolution (§1.24) — climb until
/// <c>D2.slnx</c>, <c>.git</c>, or <c>pnpm-workspace.yaml</c> is found.
/// Fixed-depth <c>..</c> walks from the test assembly path are forbidden.
/// </summary>
internal static class RepoRootFixture
{
    private static readonly string[] sr_sentinels =
    [
        "D2.slnx",
        "pnpm-workspace.yaml",
    ];

    /// <summary>
    /// Resolve monorepo root from <see cref="AppContext.BaseDirectory"/> via sentinels.
    /// </summary>
    /// <returns>Absolute monorepo root path.</returns>
    public static string Resolve()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            foreach (var sentinel in sr_sentinels)
            {
                if (File.Exists(Path.Combine(dir.FullName, sentinel)))
                {
                    return dir.FullName;
                }
            }

            if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate monorepo root by sentinel climb from "
            + AppContext.BaseDirectory);
    }

    /// <summary>
    /// True when resolution used a sentinel file or <c>.git</c> directory
    /// (not a fixed-depth relative walk).
    /// </summary>
    /// <param name="root">Candidate root path.</param>
    /// <returns>Whether any sentinel is present at <paramref name="root"/>.</returns>
    public static bool HasSentinel(string root)
    {
        foreach (var sentinel in sr_sentinels)
        {
            if (File.Exists(Path.Combine(root, sentinel)))
            {
                return true;
            }
        }

        return Directory.Exists(Path.Combine(root, ".git"));
    }
}
