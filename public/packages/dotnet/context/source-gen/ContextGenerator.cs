// -----------------------------------------------------------------------
// <copyright file="ContextGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.SourceGen;

using System;
using System.Linq;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits all context types from
/// the JSON spec files. Reads BOTH context spec files via
/// <c>AdditionalFiles</c>; dispatches per assembly:
/// <list type="bullet">
///   <item>
///     <c>DcsvIo.D2.AuthContext.Abstractions</c> → <c>IAuthContext.g.cs</c>.
///   </item>
///   <item>
///     <c>DcsvIo.D2.Context.Abstractions</c> → <c>IRequestContext.g.cs</c>,
///     <c>MutableRequestContext.g.cs</c>, <c>PropagatedContext.g.cs</c>,
///     <c>PropagatedContextExtensions.g.cs</c>,
///     <c>PropagatedContextSerializer.g.cs</c>.
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// Per-field <c>maxLength</c> caps + the <c>propagate: true</c> field
/// subset are spec-driven — codegen reads them and bakes the wire-format
/// validator + projection extensions into the abstractions assembly.
/// </summary>
[Generator]
public sealed class ContextGenerator : IIncrementalGenerator
{
    private const string _SPEC_SUFFIX = ".spec.json";

    private const string _AUTH_CONTEXT_ASSEMBLY = "DcsvIo.D2.AuthContext.Abstractions";

    private const string _CONTEXT_ABSTRACTIONS_ASSEMBLY =
        "DcsvIo.D2.Context.Abstractions";

    private const string _AUTH_SPEC_NAME = "IAuthContext";

    private const string _REQUEST_SPEC_NAME = "IRequestContext";

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

            // Only the two target assemblies get emission.
            var isAuthTarget = string.Equals(
                assemblyName, _AUTH_CONTEXT_ASSEMBLY, StringComparison.Ordinal);
            var isContextAbstractionsTarget = string.Equals(
                assemblyName, _CONTEXT_ABSTRACTIONS_ASSEMBLY, StringComparison.Ordinal);

            if (!isAuthTarget && !isContextAbstractionsTarget)
                return;

            if (specFiles.IsDefaultOrEmpty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingSpecFile, Location.None, assemblyName));
                return;
            }

            // Parse every spec file present.
            ContextSpec? authSpec = null;
            ContextSpec? requestSpec = null;
            foreach (var file in specFiles.OrderBy(s => s.Path, StringComparer.Ordinal))
            {
                var loadResult = SpecLoader.Load(file.Path, file.Content);
                if (loadResult.Diagnostic is { } diag)
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(diag));
                    continue;
                }

                var spec = loadResult.Spec!;
                if (string.Equals(spec.Name, _AUTH_SPEC_NAME, StringComparison.Ordinal))
                    authSpec = spec;
                else if (string.Equals(spec.Name, _REQUEST_SPEC_NAME, StringComparison.Ordinal))
                    requestSpec = spec;
            }

            // Dispatch per target.
            if (isAuthTarget)
            {
                if (authSpec is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingSpecFile, Location.None, assemblyName));
                    return;
                }

                EmitAndAddSource(spc, InterfaceEmitter.Emit(authSpec));
                return;
            }

            if (isContextAbstractionsTarget)
            {
                if (authSpec is null || requestSpec is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingSpecFile, Location.None, assemblyName));
                    return;
                }

                // Validate the extends chain resolves.
                if (requestSpec.Extends.Truthy())
                {
                    const string expectedExtends
                        = $"DcsvIo.D2.AuthContext.Abstractions.{_AUTH_SPEC_NAME}";
                    if (!string.Equals(
                            requestSpec.Extends, expectedExtends, StringComparison.Ordinal))
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.UnresolvableExtends,
                            Location.None,
                            requestSpec.Name,
                            requestSpec.Extends!));
                    }
                }

                EmitAndAddSource(spc, InterfaceEmitter.Emit(requestSpec));
                EmitAndAddSource(spc, MutableEmitter.Emit(authSpec, requestSpec));

                foreach (var emit in PropagatedEmitter.EmitAll(authSpec, requestSpec))
                    EmitAndAddSource(spc, emit);
            }
        });
    }

    private static bool IsSpecFile(string path) =>
        path.EndsWith(_SPEC_SUFFIX, StringComparison.OrdinalIgnoreCase);

    private static void EmitAndAddSource(SourceProductionContext spc, EmitResult result)
    {
        foreach (var d in result.Diagnostics)
            spc.ReportDiagnostic(ToRoslynDiagnostic(d));

        spc.AddSource(
            result.HintName,
            SourceText.From(result.GeneratedSource, System.Text.Encoding.UTF8));
    }

    private static Diagnostic ToRoslynDiagnostic(EmitDiagnostic emitDiag)
    {
        var descriptor = ResolveDescriptor(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.UnknownType => DiagnosticDescriptors.UnknownType,
        DiagnosticIds.PropertyNameCollision => DiagnosticDescriptors.PropertyNameCollision,
        DiagnosticIds.UnresolvableExtends => DiagnosticDescriptors.UnresolvableExtends,
        DiagnosticIds.UnknownDerivedRule => DiagnosticDescriptors.UnknownDerivedRule,
        DiagnosticIds.MissingSpecFile => DiagnosticDescriptors.MissingSpecFile,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
