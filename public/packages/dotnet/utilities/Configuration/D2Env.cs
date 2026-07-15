// -----------------------------------------------------------------------
// <copyright file="D2Env.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Configuration;

using dotenv.net;

/// <summary>
/// Convention-based <c>.env*</c> file loader for D2, intended for
/// host-side scenarios (tests, IDE debug, ad-hoc <c>dotnet run</c>) where
/// Docker Compose's native <c>env_file:</c> injection is not in play.
/// </summary>
/// <remarks>
/// <para>
/// <b>Discovery</b>: walks up from <see cref="AppContext.BaseDirectory"/>
/// (max 12 levels) looking for the FIRST directory that contains AT LEAST
/// ONE of the named files, then loads every matching file from THAT
/// directory only — never mixes files from different directories.
/// </para>
/// <para>
/// <b>Default file list</b>:
/// <c>[ ".env", ".env.local", ".env.secrets" ]</c> — matches the Docker
/// Compose layout (<c>--env-file .env.local --env-file .env.secrets</c>).
/// Pass an explicit list to override (callers can also reorder, restrict,
/// or extend).
/// </para>
/// <para>
/// <b>Precedence rules</b>:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>Process env wins over every file.</b> Any environment variable
///     that was already set when <see cref="Load"/> was invoked (containers,
///     IDE-injected vars, parent shell) is preserved unchanged. Files cannot
///     overwrite container / parent values.
///   </description></item>
///   <item><description>
///     <b>Within file loading, later files in the list override earlier ones.</b>
///     With the default list, <c>.env.secrets</c> values override
///     <c>.env.local</c> values, which override <c>.env</c> values — matching
///     Compose's <c>--env-file</c> ordering semantics.
///   </description></item>
/// </list>
/// <para>
/// <b>Idempotency</b>: <see cref="Load"/> is safe to call multiple times.
/// Subsequent calls are no-ops (the underlying file system is not re-walked).
/// </para>
/// </remarks>
public static class D2Env
{
    private const int _MAX_DEPTH = 12;

    private static readonly string[] sr_defaultFileNames =
        [".env", ".env.local", ".env.secrets"];

    private static readonly StringComparer sr_envKeyComparer =
        ResolveEnvKeyComparer(OperatingSystem.IsWindows());

    private static volatile bool s_loaded;

    /// <summary>
    /// Loads environment variables from one or more <c>.env*</c> files found
    /// at the nearest discovery directory.
    /// </summary>
    /// <param name="fileNames">
    /// Optional ordered list of file names to load. Empty (default) =
    /// <c>[ ".env", ".env.local", ".env.secrets" ]</c>. Order matters: later
    /// files in the list override earlier ones for any given key.
    /// </param>
    /// <remarks>
    /// See type-level remarks for full discovery and precedence semantics.
    /// </remarks>
    public static void Load(params string[] fileNames)
    {
        if (s_loaded)
            return;

        s_loaded = true;

        if (fileNames.Length == 0)
            fileNames = sr_defaultFileNames;

        var dir = FindDirectoryWithAnyFile(
            new DirectoryInfo(AppContext.BaseDirectory),
            fileNames);

        if (dir is null)
            return;

        var preExisting = SnapshotEnvKeys();

        foreach (var name in fileNames)
        {
            var path = Path.Combine(dir.FullName, name);
            if (!File.Exists(path))
                continue;

            var vars = DotEnv.Read(new DotEnvOptions(envFilePaths: [path]));
            ApplyVars(vars, preExisting);
        }
    }

    /// <summary>
    /// Resets the loaded flag so the next call to <see cref="Load"/> walks
    /// the directory tree again. Test-only.
    /// </summary>
    internal static void ResetForTests() => s_loaded = false;

    /// <summary>
    /// Walks up from <paramref name="startDir"/> looking for the FIRST
    /// directory (within <see cref="_MAX_DEPTH"/> levels) that contains at
    /// least one of <paramref name="fileNames"/>. Returns that directory, or
    /// <c>null</c> when no candidate is found.
    /// </summary>
    /// <param name="startDir">The directory at which the walk begins.</param>
    /// <param name="fileNames">The file names to look for.</param>
    /// <returns>The discovered directory, or <c>null</c>.</returns>
    internal static DirectoryInfo? FindDirectoryWithAnyFile(
        DirectoryInfo? startDir,
        IReadOnlyList<string> fileNames)
    {
        var dir = startDir;
        for (var i = 0; i < _MAX_DEPTH && dir is not null; i++, dir = dir.Parent)
        {
            foreach (var name in fileNames)
            {
                if (File.Exists(Path.Combine(dir.FullName, name)))
                    return dir;
            }
        }

        return null;
    }

    /// <summary>
    /// Applies <paramref name="vars"/> to the process environment, skipping
    /// any key that was present in <paramref name="preExisting"/> at the
    /// start of the load (process-env-wins-over-files semantics). Within a
    /// single call to <see cref="Load"/>, later invocations of this method
    /// (i.e. later files in the configured list) overwrite earlier ones.
    /// </summary>
    /// <param name="vars">Source key/value pairs from one file.</param>
    /// <param name="preExisting">
    /// Environment-variable keys that existed before <see cref="Load"/> was
    /// called. Any key present here is left untouched.
    /// </param>
    internal static void ApplyVars(
        IDictionary<string, string> vars,
        IReadOnlySet<string> preExisting)
    {
        foreach (var (key, value) in vars)
        {
            if (preExisting.Contains(key))
                continue;

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    /// <summary>
    /// Returns the platform-appropriate env-var-key comparer
    /// (case-insensitive on Windows, exact-match elsewhere). Test seam.
    /// </summary>
    /// <param name="isWindows">True when running on Windows.</param>
    internal static StringComparer ResolveEnvKeyComparer(bool isWindows)
        => isWindows ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>
    /// Snapshots the current set of environment-variable keys using the
    /// platform-appropriate comparer (case-insensitive on Windows, exact
    /// match elsewhere).
    /// </summary>
    internal static IReadOnlySet<string> SnapshotEnvKeys()
    {
        var snapshot = new HashSet<string>(sr_envKeyComparer);
        foreach (var key in Environment.GetEnvironmentVariables().Keys)
        {
            if (key is string s)
                snapshot.Add(s);
        }

        return snapshot;
    }
}
