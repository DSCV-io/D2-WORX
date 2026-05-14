// -----------------------------------------------------------------------
// <copyright file="ErrorCodesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.ErrorCodes.SourceGen;

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>AuthErrorCodes</c>
/// constants class + the <c>AuthFailures</c> factory class into
/// <c>D2.Shared.Auth</c> by reading
/// <c>contracts/auth-error-codes/auth-error-codes.spec.json</c> via
/// <c>AdditionalFiles</c>. Single-target — only emits when the consuming
/// assembly is <c>D2.Shared.Auth</c>.
/// </summary>
[Generator]
public sealed class ErrorCodesGenerator : IIncrementalGenerator
{
    private const string _ERROR_CODES_SOURCE_NAME = "AuthErrorCodes.g.cs";
    private const string _FAILURES_SOURCE_NAME = "AuthFailures.g.cs";
    private const string _SPEC_FILE_NAME = "auth-error-codes.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "D2.Shared.Auth";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to auth-error-codes.spec.json files.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission by assembly name.
        var combined = specs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, drive the loader + both emitters.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            // Only emit when the consuming assembly is D2.Shared.Auth.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: only one auth-error-codes.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = ErrorCodeSpecLoader.Load(spec.Path, spec.Content);
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

            var factoriesResult = FailureFactoriesEmitter.Emit(loadResult.Spec!);
            foreach (var d in factoriesResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                _FAILURES_SOURCE_NAME,
                SourceText.From(factoriesResult.GeneratedSource, System.Text.Encoding.UTF8));
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
        DiagnosticIds.UnknownCategoryEnum => DiagnosticDescriptors.UnknownCategoryEnum,
        DiagnosticIds.DuplicateCode => DiagnosticDescriptors.DuplicateCode,
        DiagnosticIds.DuplicateFactoryName => DiagnosticDescriptors.DuplicateFactoryName,
        DiagnosticIds.InvalidHttpStatus => DiagnosticDescriptors.InvalidHttpStatus,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
