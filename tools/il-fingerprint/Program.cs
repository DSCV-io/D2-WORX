// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using D2.Tools.IlFingerprint;

// CLI shape:
//   dotnet run --project tools/il-fingerprint -- <path-to-built-dll>
//
// One positional argument: the path to a built .NET assembly. The normalized,
// platform-independent metadata + IL dump is written to stdout (UTF-8, LF). The
// release-runner's fingerprint compose step captures stdout and hashes it
// alongside the PublicAPI.*.txt + manifest metadata. The dump is deterministic
// for identical source + toolchain regardless of build path / machine / OS.

if (args.Length == 1 && args[0] is "--help" or "-h" or "/?")
{
    PrintUsage();
    return 0;
}

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine("Error: exactly one argument is required — the path to a built DLL.");
    PrintUsage();
    return 2;
}

var dllPath = args[0];

if (!File.Exists(dllPath))
{
    Console.Error.WriteLine($"Error: assembly not found: {dllPath}");
    return 2;
}

try
{
    var dump = IlDumper.Dump(dllPath);

    // Emit verbatim with LF endings. Use the raw stdout stream so the platform
    // newline translation does not rewrite the LF endings to CRLF on Windows.
    var bytes = System.Text.Encoding.UTF8.GetBytes(dump);
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(bytes, 0, bytes.Length);
    stdout.Flush();

    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"il-fingerprint failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("D2.Tools.IlFingerprint");
    Console.WriteLine();
    Console.WriteLine("Emits a normalized, platform-independent metadata + IL dump of a built");
    Console.WriteLine(".NET assembly to stdout, for use as the .NET output fingerprint in the");
    Console.WriteLine("release-runner artifact-diff versioning engine.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tools/il-fingerprint -- <path-to-built-dll>");
}
