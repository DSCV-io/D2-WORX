// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

using global::D2.Tools.LoggerMessageSplitter;

// CLI shape:
//   dotnet run --project tools/loggermessage-splitter -- \
//     --input  <path-to-combined-LoggerMessage.g.cs> \
//     --output-dir <where-to-write-split-files>
//
// Both args required. Absolute or relative paths both work; relative paths
// resolve against the current working directory (MSBuild invokes from the
// consumer csproj's directory).

string? inputPath = null;
string? outputDir = null;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--input" when i + 1 < args.Length:
            inputPath = args[++i];
            break;
        case "--output-dir" when i + 1 < args.Length:
            outputDir = args[++i];
            break;
        case "--help" or "-h" or "/?":
            PrintUsage();
            return 0;
    }
}

if (string.IsNullOrWhiteSpace(inputPath) || string.IsNullOrWhiteSpace(outputDir))
{
    Console.Error.WriteLine("Error: both --input and --output-dir are required.");
    PrintUsage();
    return 2;
}

if (!File.Exists(inputPath))
{
    // Not a hard error — the MSBuild target uses Condition="Exists(...)" to
    // gate invocation, but if the consumer csproj has no LoggerMessage attrs
    // yet, the input might be missing on a clean tree. Be quiet about it.
    Console.WriteLine($"Input file not present (nothing to split): {inputPath}");
    return 0;
}

try
{
    var count = SplitterEngine.Run(inputPath, outputDir);
    Console.WriteLine($"Split LoggerMessage.g.cs into {count} per-class file(s) under: {outputDir}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Splitter failed: {ex.GetType().Name}: {ex.Message}");
    return 1;
}

static void PrintUsage()
{
    Console.WriteLine("global::D2.Tools.LoggerMessageSplitter");
    Console.WriteLine();
    Console.WriteLine("Splits Microsoft.Extensions.Logging.Generators's combined LoggerMessage.g.cs");
    Console.WriteLine("into one deterministic file per partial class.");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project tools/loggermessage-splitter -- \\");
    Console.WriteLine("    --input <path-to-combined-LoggerMessage.g.cs> \\");
    Console.WriteLine("    --output-dir <directory-to-write-split-files-to>");
}
