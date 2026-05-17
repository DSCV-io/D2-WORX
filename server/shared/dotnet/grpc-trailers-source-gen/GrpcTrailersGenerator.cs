// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Grpc.Trailers.SourceGen;

using System;
using System.IO;
using System.Linq;
using D2.Shared.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>D2GrpcTrailers</c>
/// static class into <c>D2.Shared.Auth.Grpc</c> by reading
/// <c>contracts/grpc-trailers/grpc-trailers.spec.json</c> via
/// <c>AdditionalFiles</c>. Single-target — only emits when the consuming
/// assembly is <c>D2.Shared.Auth.Grpc</c>.
/// </summary>
[Generator]
public sealed class GrpcTrailersGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "D2GrpcTrailers.g.cs";
    private const string _SPEC_FILE_NAME = "grpc-trailers.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "D2.Shared.Auth.Grpc";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to grpc-trailers.spec.json files.
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

            // Only emit when the consuming assembly is D2.Shared.Auth.Grpc.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: only one grpc-trailers.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = GrpcTrailersSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = GrpcTrailersEmitter.Emit(loadResult.Spec!);
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
        DiagnosticIds.DuplicateConstName => DiagnosticDescriptors.DuplicateConstName,
        DiagnosticIds.DuplicateValue => DiagnosticDescriptors.DuplicateValue,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.EmptyValue => DiagnosticDescriptors.EmptyValue,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
