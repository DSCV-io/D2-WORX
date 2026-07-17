// -----------------------------------------------------------------------
// <copyright file="DlqFailureMetadataGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

using System;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits dlq-failure-metadata
/// catalog artifacts. Multi-target dispatch:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.Messaging.Abstractions</c> →
///     <c>DlqFailureMetadataFields.g.cs</c> (Fields catalog).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Messaging.RabbitMq</c> → <c>DlqFailureCauses.g.cs</c>
///     (Causes catalog, since <c>DlqFailureHeaderBuilder</c> lives there).
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// </summary>
[Generator]
public sealed class DlqFailureMetadataGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "dlq-failure-metadata.spec.json";

    private const string _FIELDS_ASSEMBLY = "DcsvIo.D2.Messaging.Abstractions";
    private const string _CAUSES_ASSEMBLY = "DcsvIo.D2.Messaging.RabbitMq";

    private const string _FIELDS_SOURCE_NAME = "DlqFailureMetadataFields.g.cs";
    private const string _CAUSES_SOURCE_NAME = "DlqFailureCauses.g.cs";

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

            // Dispatch on assembly name — anything other than the two
            // target assemblies emits nothing.
            if (!string.Equals(assemblyName, _FIELDS_ASSEMBLY, StringComparison.Ordinal) &&
                !string.Equals(assemblyName, _CAUSES_ASSEMBLY, StringComparison.Ordinal))
            {
                return;
            }

            if (specFiles.IsDefaultOrEmpty)
                return;

            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = DlqFailureMetadataSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            if (string.Equals(assemblyName, _FIELDS_ASSEMBLY, StringComparison.Ordinal))
            {
                var emitResult = DlqFieldsEmitter.EmitFieldsCatalog(loadResult.Spec!);
                foreach (var d in emitResult.Diagnostics)
                    spc.ReportDiagnostic(ToRoslynDiagnostic(d));

                spc.AddSource(
                    _FIELDS_SOURCE_NAME,
                    SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
            }
            else if (string.Equals(assemblyName, _CAUSES_ASSEMBLY, StringComparison.Ordinal))
            {
                var emitResult = DlqCausesEmitter.Emit(loadResult.Spec!);
                foreach (var d in emitResult.Diagnostics)
                    spc.ReportDiagnostic(ToRoslynDiagnostic(d));

                spc.AddSource(
                    _CAUSES_SOURCE_NAME,
                    SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
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
        DiagnosticIds.DuplicateFieldConstName => DiagnosticDescriptors.DuplicateFieldConstName,
        DiagnosticIds.DuplicateFieldValue => DiagnosticDescriptors.DuplicateFieldValue,
        DiagnosticIds.DuplicateCause => DiagnosticDescriptors.DuplicateCause,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.EmptyValue => DiagnosticDescriptors.EmptyValue,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
