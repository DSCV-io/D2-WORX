// -----------------------------------------------------------------------
// <copyright file="ErrorCodesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ResultErrorCodes.SourceGen;

using System;
using System.IO;
using System.Linq;
using D2.Shared.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>ErrorCodes</c>
/// constants class into <c>D2.Shared.Result</c> by reading
/// <c>contracts/error-codes/error-codes.spec.json</c> via
/// <c>AdditionalFiles</c>. Single-target — only emits when the consuming
/// assembly is <c>D2.Shared.Result</c>.
/// </summary>
[Generator]
public sealed class ErrorCodesGenerator : IIncrementalGenerator
{
    private const string _ERROR_CODES_SOURCE_NAME = "ErrorCodes.g.cs";
    private const string _SPEC_FILE_NAME = "error-codes.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "D2.Shared.Result";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to error-codes.spec.json files.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission by assembly name.
        var combined = specs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, drive the loader + emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            // Only emit when the consuming assembly is D2.Shared.Result.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: only one error-codes.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = ErrorCodesSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var codesResult = ErrorCodesEmitter.Emit(loadResult.Spec!);
            foreach (var d in codesResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                _ERROR_CODES_SOURCE_NAME,
                SourceText.From(codesResult.GeneratedSource, System.Text.Encoding.UTF8));
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
        DiagnosticIds.DuplicateCode => DiagnosticDescriptors.DuplicateCode,
        DiagnosticIds.InvalidHttpStatus => DiagnosticDescriptors.InvalidHttpStatus,
        DiagnosticIds.InvalidCode => DiagnosticDescriptors.InvalidCode,
        DiagnosticIds.MissingDoc => DiagnosticDescriptors.MissingDoc,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
