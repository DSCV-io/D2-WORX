// -----------------------------------------------------------------------
// <copyright file="TelemetryTagsGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits per-meter
/// <c>*TelemetryTags.g.cs</c> typed-constants classes by reading
/// <c>contracts/telemetry/telemetry.spec.json</c> via <c>AdditionalFiles</c>.
/// Per-meter single-target dispatch — emits ONLY when the consuming assembly
/// matches the meter's <c>consumingAssembly</c> field.
/// </summary>
[Generator]
public sealed class TelemetryTagsGenerator : IIncrementalGenerator
{
    private const string _TELEMETRY_SPEC_FILE_NAME = "telemetry.spec.json";

    private static readonly HashSet<string> sr_recognizedSpecFileNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            _TELEMETRY_SPEC_FILE_NAME,
            CrossSpecResolver.AUTH_ERROR_CODES_SPEC_FILE_NAME,
        };

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to spec files we care about (the telemetry
        //    spec + sibling specs the cross-spec resolver may need to read).
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsRecognizedSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission per-meter
        //    by consumingAssembly.
        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            // Locate the telemetry spec (skip the sibling cross-spec files when
            // picking the spec we're driven by).
            SpecFile? telemetrySpecFile = null;
            var siblingSpecs = ImmutableArray.CreateBuilder<SpecFile>();
            foreach (var f in specFiles.OrderBy(s => s.Path, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(f.Path);
                if (string.Equals(
                    name,
                    _TELEMETRY_SPEC_FILE_NAME,
                    StringComparison.OrdinalIgnoreCase))
                {
                    telemetrySpecFile ??= f;
                }
                else
                {
                    siblingSpecs.Add(f);
                }
            }

            if (telemetrySpecFile is null)
                return;

            var loadResult = TelemetrySpecLoader.Load(
                telemetrySpecFile.Path, telemetrySpecFile.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var spec = loadResult.Spec!;
            var assemblyName = compilation.AssemblyName ?? string.Empty;

            // Cross-spec uniqueness — surfaced once per duplicate meter name.
            var seenMeters = new HashSet<string>(StringComparer.Ordinal);
            foreach (var meter in spec.Meters)
            {
                if (!seenMeters.Add(meter.Meter))
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(
                        EmitDiagnostics.DuplicateMeter(meter.Meter)));
                }
            }

            var siblings = siblingSpecs.ToImmutable();

            foreach (var meter in spec.Meters)
            {
                // Single-target dispatch — only emit for the meter whose
                // consumingAssembly matches the current compilation.
                if (!string.Equals(
                    meter.ConsumingAssembly, assemblyName, StringComparison.Ordinal))
                {
                    continue;
                }

                var emitResult = TelemetryTagsEmitter.Emit(meter, siblings);
                foreach (var d in emitResult.Diagnostics)
                    spc.ReportDiagnostic(ToRoslynDiagnostic(d));

                if (emitResult.GeneratedSource.Length > 0)
                {
                    spc.AddSource(
                        emitResult.HintName,
                        SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
                }
            }
        });
    }

    private static bool IsRecognizedSpecFile(string path) =>
        sr_recognizedSpecFileNames.Contains(Path.GetFileName(path));

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.DuplicateMeter => DiagnosticDescriptors.DuplicateMeter,
        DiagnosticIds.DuplicateInstrument => DiagnosticDescriptors.DuplicateInstrument,
        DiagnosticIds.UnknownInstrumentKind => DiagnosticDescriptors.UnknownInstrumentKind,
        DiagnosticIds.DuplicateTagValue => DiagnosticDescriptors.DuplicateTagValue,
        DiagnosticIds.CrossSpecInconsistency => DiagnosticDescriptors.CrossSpecInconsistency,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
