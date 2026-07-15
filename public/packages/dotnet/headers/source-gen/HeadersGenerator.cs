// -----------------------------------------------------------------------
// <copyright file="HeadersGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits per-transport header
/// catalog classes by reading <c>contracts/headers/headers.spec.json</c>
/// via <c>AdditionalFiles</c>. Dispatches per consuming assembly:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.Headers.Common</c> → <c>CommonHeaders.g.cs</c>
///     (entries with applicability count &gt;= 2).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Headers.Http</c> → <c>HttpHeaders.g.cs</c>
///     (entries whose applicability includes <c>http</c>).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Headers.Amqp</c> → <c>AmqpHeaders.g.cs</c>
///     (entries whose applicability includes <c>amqp</c>).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Headers.Grpc</c> → <c>GrpcHeaders.g.cs</c>
///     (entries whose applicability includes <c>grpc</c>).
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// Cross-transport entries appear in multiple per-transport catalogs at
/// identical wire values, codegen-guaranteed.
/// </summary>
[Generator]
public sealed class HeadersGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "headers.spec.json";

    private const string _COMMON_ASSEMBLY = "DcsvIo.D2.Headers.Common";
    private const string _HTTP_ASSEMBLY = "DcsvIo.D2.Headers.Http";
    private const string _AMQP_ASSEMBLY = "DcsvIo.D2.Headers.Amqp";
    private const string _GRPC_ASSEMBLY = "DcsvIo.D2.Headers.Grpc";

    private static readonly Dictionary<string, DispatchEntry> sr_dispatch =
        new(StringComparer.Ordinal)
        {
            [_COMMON_ASSEMBLY] = new(
                HeadersEmitter.CatalogFilter.Common,
                "CommonHeaders",
                "CommonHeaders.g.cs"),
            [_HTTP_ASSEMBLY] = new(
                HeadersEmitter.CatalogFilter.Http,
                "HttpHeaders",
                "HttpHeaders.g.cs"),
            [_AMQP_ASSEMBLY] = new(
                HeadersEmitter.CatalogFilter.Amqp,
                "AmqpHeaders",
                "AmqpHeaders.g.cs"),
            [_GRPC_ASSEMBLY] = new(
                HeadersEmitter.CatalogFilter.Grpc,
                "GrpcHeaders",
                "GrpcHeaders.g.cs"),
        };

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

            if (!sr_dispatch.TryGetValue(assemblyName, out var dispatch))
                return;

            if (specFiles.IsDefaultOrEmpty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingSpec, Location.None, assemblyName));
                return;
            }

            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();
            var loadResult = HeadersSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = HeadersEmitter.Emit(
                loadResult.Spec!,
                dispatch.Filter,
                assemblyName,
                dispatch.ClassName);

            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                dispatch.SourceName,
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
        DiagnosticIds.UnknownTransport => DiagnosticDescriptors.UnknownTransport,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.DuplicateConstName => DiagnosticDescriptors.DuplicateConstName,
        DiagnosticIds.EmptyApplicability => DiagnosticDescriptors.EmptyApplicability,
        DiagnosticIds.UnknownConvention => DiagnosticDescriptors.UnknownConvention,
        DiagnosticIds.MissingSpec => DiagnosticDescriptors.MissingSpec,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private readonly record struct DispatchEntry(
        HeadersEmitter.CatalogFilter Filter,
        string ClassName,
        string SourceName);
}
