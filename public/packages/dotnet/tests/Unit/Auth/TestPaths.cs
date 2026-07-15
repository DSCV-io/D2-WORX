// -----------------------------------------------------------------------
// <copyright file="TestPaths.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Auth;

using System;
using System.IO;

/// <summary>
/// Locates spec JSON files under dual contract roots by walking up from the
/// test bin directory until root <c>D2.slnx</c> is found.
/// </summary>
internal static class TestPaths
{
    /// <summary>
    /// Returns the absolute path to the monorepo root (directory containing
    /// <c>D2.slnx</c>), or throws if not found.
    /// </summary>
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null)
        {
            var marker = Path.Combine(dir.FullName, "D2.slnx");
            if (File.Exists(marker))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate repo root by walking up from " + AppContext.BaseDirectory);
    }

    public static string PublicContractsRoot() =>
        Path.Combine(RepoRoot(), "public", "contracts");

    public static string PrivateContractsRoot() =>
        Path.Combine(RepoRoot(), "private", "contracts");

    public static string AuthScopesSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-scopes", "scopes.spec.json");

    public static string AuthContextSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-context", "IAuthContext.spec.json");

    public static string RequestContextSpec() =>
        Path.Combine(PublicContractsRoot(), "request-context", "IRequestContext.spec.json");

    public static string HeadersSpec() =>
        Path.Combine(PublicContractsRoot(), "headers", "headers.spec.json");

    public static string InProcessKeysSpec() =>
        Path.Combine(PublicContractsRoot(), "in-process-keys", "keys.spec.json");

    public static string JwtClaimsSpec() =>
        Path.Combine(PublicContractsRoot(), "jwt-claims", "jwt-claims.spec.json");

    public static string AuthErrorCodesSpec() =>
        Path.Combine(PublicContractsRoot(), "auth-error-codes", "auth-error-codes.spec.json");

    public static string MessagesDirectory() =>
        Path.Combine(PublicContractsRoot(), "messages");

    public static string PrivateMessagesDirectory() =>
        Path.Combine(PrivateContractsRoot(), "messages");

    public static string ErrorCategorySpec() =>
        Path.Combine(PublicContractsRoot(), "error-category", "error-category.spec.json");
}
