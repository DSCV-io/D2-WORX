// -----------------------------------------------------------------------
// <copyright file="InProcessKeysGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.InProcessKeys.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits binding-specific
/// in-process slot-key catalog classes by reading
/// <c>contracts/in-process-keys/keys.spec.json</c> via
/// <c>AdditionalFiles</c>. Dispatches per consuming assembly:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.Auth.Abstractions</c> →
///     <c>Http/D2HttpContextItems.g.cs</c> (entries with binding <c>http</c>;
///     class is <c>public</c>, namespace <c>DcsvIo.D2.Auth.Abstractions.Http</c>).
///   </item>
///   <item>
///     <c>DcsvIo.D2.Auth.Grpc</c> →
///     <c>Interceptors/D2GrpcUserStateKeys.g.cs</c> (entries with binding
///     <c>grpc</c>; class is <c>internal</c>, namespace
///     <c>DcsvIo.D2.Auth.Grpc.Interceptors</c>).
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// Cross-binding entries appear in both catalogs at identical wire values,
/// codegen-guaranteed (verified by
/// <c>HttpContextItemsVsGrpcUserStateKeysConsistencyTests</c>).
/// </summary>
[Generator]
public sealed class InProcessKeysGenerator : IIncrementalGenerator
{
    private const string _SPEC_FILE_NAME = "keys.spec.json";

    private const string _HTTP_CONSUMING_ASSEMBLY = "DcsvIo.D2.Auth.Abstractions";
    private const string _GRPC_CONSUMING_ASSEMBLY = "DcsvIo.D2.Auth.Grpc";

    private static readonly Dictionary<string, DispatchTarget> sr_dispatch =
        new(StringComparer.Ordinal)
        {
            [_HTTP_CONSUMING_ASSEMBLY] = new DispatchTarget(
                Filter: InProcessKeysEmitter.BindingFilter.Http,
                TargetNamespace: "DcsvIo.D2.Auth.Abstractions.Http",
                ClassName: "D2HttpContextItems",
                Visibility: "public",
                SourceName: "D2HttpContextItems.g.cs"),
            [_GRPC_CONSUMING_ASSEMBLY] = new DispatchTarget(
                Filter: InProcessKeysEmitter.BindingFilter.Grpc,
                TargetNamespace: "DcsvIo.D2.Auth.Grpc.Interceptors",
                ClassName: "D2GrpcUserStateKeys",
                Visibility: "internal",
                SourceName: "D2GrpcUserStateKeys.g.cs"),
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
            var loadResult = InProcessKeysSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = InProcessKeysEmitter.Emit(
                loadResult.Spec!,
                dispatch.Filter,
                dispatch.TargetNamespace,
                dispatch.ClassName,
                dispatch.Visibility);

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
        DiagnosticIds.UnknownBinding => DiagnosticDescriptors.UnknownBinding,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.MissingSpec => DiagnosticDescriptors.MissingSpec,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private sealed record DispatchTarget(
        InProcessKeysEmitter.BindingFilter Filter,
        string TargetNamespace,
        string ClassName,
        string Visibility,
        string SourceName);
}
