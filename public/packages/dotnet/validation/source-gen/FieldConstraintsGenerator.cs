// -----------------------------------------------------------------------
// <copyright file="FieldConstraintsGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Validation.SourceGen;

using System;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>FieldConstraints</c>
/// const-int catalog (<c>FieldConstraints.g.cs</c>) and the closed-list taxonomy
/// enums (<c>Taxonomy.g.cs</c>) into <c>DcsvIo.D2.Validation.Abstractions</c>
/// by reading <c>contracts/validation/field-constraints.spec.json</c> via
/// <c>AdditionalFiles</c>. Single-target — only emits when the consuming
/// assembly is <c>DcsvIo.D2.Validation.Abstractions</c>.
/// </summary>
[Generator]
public sealed class FieldConstraintsGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "field-constraints.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Validation.Abstractions";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to field-constraints.spec.json files.
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

            // Only emit when the consuming assembly is the validation
            // abstractions library.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: only one field-constraints.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = FieldConstraintsSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var results = FieldConstraintsEmitter.Emit(loadResult.Spec!);
            foreach (var result in results)
            {
                foreach (var d in result.Diagnostics)
                    spc.ReportDiagnostic(ToRoslynDiagnostic(d));

                spc.AddSource(
                    result.HintName,
                    SourceText.From(result.GeneratedSource, System.Text.Encoding.UTF8));
            }
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
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.NonPositiveValue => DiagnosticDescriptors.NonPositiveValue,
        DiagnosticIds.DuplicateEnumName => DiagnosticDescriptors.DuplicateEnumName,
        DiagnosticIds.InvalidEnumName => DiagnosticDescriptors.InvalidEnumName,
        DiagnosticIds.EmptyEnumMemberList => DiagnosticDescriptors.EmptyEnumMemberList,
        DiagnosticIds.DuplicateEnumMember => DiagnosticDescriptors.DuplicateEnumMember,
        DiagnosticIds.InvalidEnumMemberName => DiagnosticDescriptors.InvalidEnumMemberName,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
