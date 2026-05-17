// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.ProblemDetails.SourceGen;

using System;
using System.IO;
using System.Linq;
using D2.Shared.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the static class
/// <c>D2ProblemDetailsKeys</c> carrying the spec-derived constants
/// (<c>TYPE_URI_PREFIX</c>, <c>CONTENT_TYPE</c>, <c>EXTENSION_*</c>,
/// <c>TITLE_*</c>) + the <c>TitleFor</c> switch into
/// <c>D2.Shared.ProblemDetails.Abstractions</c> by reading
/// <c>contracts/problem-details/problem-details.spec.json</c> via
/// <c>AdditionalFiles</c>. Single-target — only emits when the consuming
/// assembly is <c>D2.Shared.ProblemDetails.Abstractions</c>. The abstractions
/// csproj is referenced by both <c>D2.Shared.Auth.Http</c> (path A emitter)
/// and <c>D2.Shared.AspNetCore</c> (path B Customizer), so a single emitted
/// constant set serves both .NET ProblemDetails wire paths.
/// </summary>
[Generator]
public sealed class ProblemDetailsGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "D2ProblemDetailsKeys.g.cs";
    private const string _SPEC_FILE_NAME = "problem-details.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "D2.Shared.ProblemDetails.Abstractions";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to problem-details.spec.json files.
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

            // Only emit when the consuming assembly is the abstractions csproj.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: only one problem-details.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = ProblemDetailsSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = ProblemDetailsEmitter.Emit(loadResult.Spec!);
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
        DiagnosticIds.DuplicateExtensionKeyConstName =>
            DiagnosticDescriptors.DuplicateExtensionKeyConstName,
        DiagnosticIds.DuplicateExtensionKeyValue =>
            DiagnosticDescriptors.DuplicateExtensionKeyValue,
        DiagnosticIds.DuplicateTitleConstName =>
            DiagnosticDescriptors.DuplicateTitleConstName,
        DiagnosticIds.DuplicateTitleHttpStatus =>
            DiagnosticDescriptors.DuplicateTitleHttpStatus,
        DiagnosticIds.TypeUriPrefixMissingTrailingSlash =>
            DiagnosticDescriptors.TypeUriPrefixMissingTrailingSlash,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
