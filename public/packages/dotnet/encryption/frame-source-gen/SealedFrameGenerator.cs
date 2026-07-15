// -----------------------------------------------------------------------
// <copyright file="SealedFrameGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionFrame.SourceGen;

using System;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the
/// <c>SealedFrameLayout</c> static class into <c>DcsvIo.D2.Encryption</c>
/// from the sealed-frame sibling spec. Single-target. Lives beside
/// <see cref="EncryptionFrameGenerator"/> (the symmetric version-1 catalog);
/// the two generators filter on different spec file names so neither ever
/// reads the other's catalog.
/// </summary>
[Generator]
public sealed class SealedFrameGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "SealedFrameLayout.g.cs";
    private const string _SPEC_FILE_NAME = "encryption-frame-sealed.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Encryption";

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

            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = SealedFrameSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = SealedFrameEmitter.Emit(loadResult.Spec!);
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
        DiagnosticIds.SealedMalformedSpec => DiagnosticDescriptors.SealedMalformedSpec,
        DiagnosticIds.SealedDuplicateFieldName => DiagnosticDescriptors.SealedDuplicateFieldName,
        DiagnosticIds.SealedOverlappingFields => DiagnosticDescriptors.SealedOverlappingFields,
        DiagnosticIds.SealedInvalidLength => DiagnosticDescriptors.SealedInvalidLength,
        DiagnosticIds.SealedInvalidVersion => DiagnosticDescriptors.SealedInvalidVersion,
        DiagnosticIds.SealedUnknownFieldKind => DiagnosticDescriptors.SealedUnknownFieldKind,
        DiagnosticIds.SealedBinaryLengthPrefixMissing =>
            DiagnosticDescriptors.SealedBinaryLengthPrefixMissing,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
