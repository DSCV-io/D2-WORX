// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Envelope.SourceGen;

using System;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the
/// <c>D2ResultEnvelopeFieldNames</c> catalog into <c>DcsvIo.D2.Result</c>
/// from <c>contracts/d2result-envelope/d2result-envelope.spec.json</c>.
/// Single-target dispatch on assembly name — any other consumer emits
/// nothing. The hand-written <c>D2Result</c> class consumes the emitted
/// constants via <c>[JsonPropertyName(D2ResultEnvelopeFieldNames.*)]</c>
/// attributes — single source of truth for the wire field-name strings.
/// </summary>
[Generator]
public sealed class D2ResultEnvelopeGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "d2result-envelope.spec.json";

    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.Result";

    private const string _SOURCE_NAME = "D2ResultEnvelopeFieldNames.g.cs";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;
            var assemblyName = compilation.AssemblyName ?? string.Empty;

            // Single-target dispatch — anything other than the DcsvIo.D2.Result
            // assembly emits nothing.
            if (!string.Equals(assemblyName, _TARGET_ASSEMBLY, StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = D2ResultEnvelopeSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = D2ResultEnvelopeEmitter.Emit(loadResult.Spec!);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                _SOURCE_NAME,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static bool IsSpecFile(string path) =>
        string.Equals(
            Path.GetFileName(path),
            _SPEC_FILE_NAME,
            StringComparison.OrdinalIgnoreCase);

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.DuplicateFieldConstName => DiagnosticDescriptors.DuplicateFieldConstName,
        DiagnosticIds.DuplicateFieldValue => DiagnosticDescriptors.DuplicateFieldValue,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.EmptyValue => DiagnosticDescriptors.EmptyValue,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
