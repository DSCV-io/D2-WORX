// -----------------------------------------------------------------------
// <copyright file="RepoRootFixture.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Packages.Tests.Unit.Extensions;

using System;
using System.IO;

/// <summary>
/// Locates monorepo root by walking up from the test bin until <c>D2.slnx</c>.
/// </summary>
internal static class RepoRootFixture
{
    public static string Resolve()
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
}
