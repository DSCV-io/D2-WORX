// -----------------------------------------------------------------------
// <copyright file="WireShapesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits per-wire-shape catalog
/// static classes by reading the consuming csproj's
/// <c>&lt;AdditionalFiles&gt;</c>-declared wire-shape spec files.
/// Dispatches per consuming assembly:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.I18n.Abstractions</c> → reads
///     <c>tk-message.spec.json</c> → emits <c>TkMessageWireShape.g.cs</c>.
///   </item>
///   <item>
///     <c>DcsvIo.D2.Result</c> → reads <c>input-error.spec.json</c> →
///     emits <c>InputErrorWireShape.g.cs</c>.
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// One analyzer, one shared scaffolding (loader + emitter), multiple
/// catalogs — same approach as <c>DcsvIo.D2.Headers.SourceGen</c>'s
/// per-transport dispatch.
/// </summary>
[Generator]
public sealed class WireShapesGenerator : IIncrementalGenerator
{
    private const string _TK_MESSAGE_ASSEMBLY = "DcsvIo.D2.I18n.Abstractions";
    private const string _INPUT_ERROR_ASSEMBLY = "DcsvIo.D2.Result";

    private static readonly Dictionary<string, DispatchEntry> sr_dispatch =
        new(StringComparer.Ordinal)
        {
            [_TK_MESSAGE_ASSEMBLY] = new(
                SpecFileName: "tk-message.spec.json",
                NamespaceName: "DcsvIo.D2.I18n",
                ClassName: "TkMessageWireShape",
                CatalogDescription: "TKMessage",
                SourceFileName: "TkMessageWireShape.g.cs"),
            [_INPUT_ERROR_ASSEMBLY] = new(
                SpecFileName: "input-error.spec.json",
                NamespaceName: "DcsvIo.D2.Result",
                ClassName: "InputErrorWireShape",
                CatalogDescription: "InputError",
                SourceFileName: "InputErrorWireShape.g.cs"),
        };

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Surface every wire-shape spec file the consumer added via
        //    AdditionalFiles. The dispatch happens by filename match against
        //    the target catalog assembly's expected SpecFileName.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsAnyWireShapeSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission by assembly name.
        var combined = specs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, dispatch by assembly name.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;
            var assemblyName = compilation.AssemblyName ?? string.Empty;

            if (!sr_dispatch.TryGetValue(assemblyName, out var dispatch))
                return;

            // Find the spec file matching this catalog's expected filename.
            SpecFile? matched = null;
            if (!specFiles.IsDefaultOrEmpty)
            {
                foreach (var sf in specFiles.OrderBy(s => s.Path, StringComparer.Ordinal))
                {
                    if (string.Equals(
                        Path.GetFileName(sf.Path),
                        dispatch.SpecFileName,
                        StringComparison.OrdinalIgnoreCase))
                    {
                        matched = sf;
                        break;
                    }
                }
            }

            if (matched is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingSpec, Location.None, assemblyName));
                return;
            }

            var loadResult = WireShapeSpecLoader.Load(matched.Path, matched.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = WireShapeEmitter.Emit(
                loadResult.Spec!,
                dispatch.NamespaceName,
                dispatch.ClassName,
                dispatch.CatalogDescription);

            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                dispatch.SourceFileName,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static bool IsAnyWireShapeSpec(string path)
    {
        var name = Path.GetFileName(path);
        foreach (var entry in sr_dispatch.Values)
        {
            if (string.Equals(name, entry.SpecFileName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.DuplicatePropertyConstName =>
            DiagnosticDescriptors.DuplicatePropertyConstName,
        DiagnosticIds.DuplicatePropertyValue =>
            DiagnosticDescriptors.DuplicatePropertyValue,
        DiagnosticIds.InvalidConstName =>
            DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.MissingSpec =>
            DiagnosticDescriptors.MissingSpec,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private readonly record struct DispatchEntry(
        string SpecFileName,
        string NamespaceName,
        string ClassName,
        string CatalogDescription,
        string SourceFileName);
}
