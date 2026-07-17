// -----------------------------------------------------------------------
// <copyright file="RegistryGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.Registry.SourceGen;

using System;
using System.Collections.Immutable;
using System.IO;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the merged cross-catalog
/// <c>ErrorCodeRegistry.g.cs</c> (the <c>ErrorCodeInfo</c> record and
/// <c>ErrorCodeRegistry</c> static class) into
/// <c>DcsvIo.D2.ErrorCodes.Registry</c>.
/// </summary>
/// <remarks>
/// <para>
/// The generator globs ALL error-code specs surfaced via
/// <c>AdditionalFiles</c> — the generic <c>error-codes.spec.json</c> plus
/// every <c>*-error-codes.spec.json</c> — aggregates them into a merged
/// table, runs the cross-catalog collision check (<c>D2ERC004</c> /
/// <c>D2ERC005</c>), and emits one merged file into the single target
/// assembly <c>DcsvIo.D2.ErrorCodes.Registry</c>. Anything else →
/// emit nothing.
/// </para>
/// <para>
/// The generator intentionally does NOT modify the existing per-catalog
/// generators or their emitted output, preserving byte-identity of the
/// committed <c>*.g.cs</c> files.
/// </para>
/// </remarks>
[Generator]
public sealed class RegistryGenerator : IIncrementalGenerator
{
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.ErrorCodes.Registry";
    private const string _HINT_NAME = "ErrorCodeRegistry.g.cs";
    private const string _CATEGORY_SPEC_NAME = "error-category.spec.json";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsRelevantSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Split the category spec out of the error-code specs; only the
            // latter feed the registry table. The category spec defines the
            // closed ErrorCategory set the membership check validates against.
            var categoryWireSet = ImmutableHashSet<string>.Empty
                .WithComparer(StringComparer.Ordinal);
            var errorCodeSpecs = ImmutableArray.CreateBuilder<SpecFile>();
            foreach (var spec in specFiles)
            {
                if (IsCategorySpec(spec.Path))
                    categoryWireSet = CategorySpecLoader.LoadWireSet(spec.Content);
                else
                    errorCodeSpecs.Add(spec);
            }

            if (errorCodeSpecs.Count == 0)
                return;

            var loadDiagnostics = ImmutableArray.CreateBuilder<EmitDiagnostic>();
            var entries = RegistrySpecLoader.LoadAll(
                errorCodeSpecs.ToImmutable(), loadDiagnostics);

            foreach (var diag in loadDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (loadDiagnostics.Count > 0)
                return;

            var collisionDiagnostics = RegistryCollisionChecker.Check(entries);
            foreach (var diag in collisionDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (!collisionDiagnostics.IsEmpty)
                return;

            var categoryDiagnostics = CategorySpecLoader.Check(entries, categoryWireSet);
            foreach (var diag in categoryDiagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(diag));

            if (!categoryDiagnostics.IsEmpty)
                return;

            var source = RegistryEmitter.Emit(entries);
            spc.AddSource(_HINT_NAME, SourceText.From(source, System.Text.Encoding.UTF8));
        });
    }

    private static bool IsRelevantSpec(string path) =>
        IsErrorCodeSpec(path) || IsCategorySpec(path);

    private static bool IsErrorCodeSpec(string path) =>
        RegistrySpecLoader.IsErrorCodeSpec(Path.GetFileName(path));

    private static bool IsCategorySpec(string path) =>
        string.Equals(
            Path.GetFileName(path),
            _CATEGORY_SPEC_NAME,
            StringComparison.OrdinalIgnoreCase);

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        RegistryDiagnosticIds.CrossCatalogDuplicateCode =>
            RegistryDiagnosticDescriptors.CrossCatalogDuplicateCode,
        RegistryDiagnosticIds.ReservedNamespaceViolation =>
            RegistryDiagnosticDescriptors.ReservedNamespaceViolation,
        RegistryDiagnosticIds.MalformedRegistrySpec =>
            RegistryDiagnosticDescriptors.MalformedRegistrySpec,
        RegistryDiagnosticIds.UnknownCategory =>
            RegistryDiagnosticDescriptors.UnknownCategory,
        _ => throw new InvalidOperationException(
            $"Unknown registry diagnostic id '{id}'."),
    };
}
