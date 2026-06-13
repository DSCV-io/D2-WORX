// -----------------------------------------------------------------------
// <copyright file="TestPaths.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.Auth;

using System;
using System.IO;

/// <summary>
/// Locates spec JSON files (contracts/) at runtime by walking up from the
/// test bin directory until <c>D2.slnx</c> is found. Source-of-truth specs
/// drive parity tests below; we read the live file rather than embedding a
/// snapshot so drift surfaces immediately.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// Returns the absolute path to the repo root (the directory containing
    /// <c>server/D2.slnx</c>), or throws if not found.
    /// </summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "server", "D2.slnx");
            if (File.Exists(marker))
                return dir.FullName;

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root by walking up from " + AppContext.BaseDirectory);
    }

    public static string AuthScopesSpec() =>
        Path.Combine(RepoRoot(), "contracts", "auth-scopes", "scopes.spec.json");

    public static string AuthContextSpec() =>
        Path.Combine(RepoRoot(), "contracts", "auth-context", "IAuthContext.spec.json");

    public static string RequestContextSpec() =>
        Path.Combine(RepoRoot(), "contracts", "request-context", "IRequestContext.spec.json");

    public static string HeadersSpec() =>
        Path.Combine(RepoRoot(), "contracts", "headers", "headers.spec.json");

    public static string InProcessKeysSpec() =>
        Path.Combine(RepoRoot(), "contracts", "in-process-keys", "keys.spec.json");

    public static string JwtClaimsSpec() =>
        Path.Combine(RepoRoot(), "contracts", "jwt-claims", "jwt-claims.spec.json");

    public static string AuthErrorCodesSpec() =>
        Path.Combine(
            RepoRoot(),
            "contracts",
            "auth-error-codes",
            "auth-error-codes.spec.json");

    public static string MessagesDirectory() =>
        Path.Combine(RepoRoot(), "contracts", "messages");

    public static string ErrorCategorySpec() =>
        Path.Combine(
            RepoRoot(),
            "contracts",
            "error-category",
            "error-category.spec.json");
}
