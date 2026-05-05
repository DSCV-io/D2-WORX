// -----------------------------------------------------------------------
// <copyright file="ContextGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Context.SourceGen;

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits context interfaces +
/// mutable concrete + envelope record. Reads BOTH context spec files via
/// <c>AdditionalFiles</c>; dispatches per assembly:
/// <list type="bullet">
///   <item><c>D2.Shared.AuthContext.Abstractions</c> → <c>IAuthContext.g.cs</c>.</item>
///   <item><c>D2.Shared.RequestContext.Abstractions</c> → <c>IRequestContext.g.cs</c>.</item>
///   <item>
///     <c>D2.Shared.RequestContext</c> → <c>MutableRequestContext.g.cs</c> +
///     <c>ContextEnvelope.g.cs</c>.
///   </item>
///   <item>Anything else → emit nothing.</item>
/// </list>
/// </summary>
[Generator]
public sealed class ContextGenerator : IIncrementalGenerator
{
    private const string _SPEC_SUFFIX = ".spec.json";

    private const string _AUTH_CONTEXT_ASSEMBLY = "D2.Shared.AuthContext.Abstractions";

    private const string _REQUEST_CONTEXT_ABSTRACTIONS_ASSEMBLY =
        "D2.Shared.RequestContext.Abstractions";

    private const string _REQUEST_CONTEXT_ASSEMBLY = "D2.Shared.RequestContext";

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

            // Only the three target assemblies get emission.
            var isAuthTarget = string.Equals(
                assemblyName, _AUTH_CONTEXT_ASSEMBLY, StringComparison.Ordinal);
            var isRequestAbstractionsTarget = string.Equals(
                assemblyName, _REQUEST_CONTEXT_ABSTRACTIONS_ASSEMBLY, StringComparison.Ordinal);
            var isRequestTarget = string.Equals(
                assemblyName, _REQUEST_CONTEXT_ASSEMBLY, StringComparison.Ordinal);

            if (!isAuthTarget && !isRequestAbstractionsTarget && !isRequestTarget)
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

            if (isRequestAbstractionsTarget)
            {
                if (requestSpec is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingSpecFile, Location.None, assemblyName));
                    return;
                }

                // Validate the extends chain resolves.
                if (!string.IsNullOrEmpty(requestSpec.Extends))
                {
                    const string expectedExtends
                        = $"D2.Shared.AuthContext.Abstractions.{_AUTH_SPEC_NAME}";
                    if (!string.Equals(
                            requestSpec.Extends, expectedExtends, StringComparison.Ordinal) &&
                        authSpec is null)
                    {
                        spc.ReportDiagnostic(Diagnostic.Create(
                            DiagnosticDescriptors.UnresolvableExtends,
                            Location.None,
                            requestSpec.Name,
                            requestSpec.Extends!));
                    }
                }

                EmitAndAddSource(spc, InterfaceEmitter.Emit(requestSpec));
                return;
            }

            if (isRequestTarget)
            {
                if (authSpec is null || requestSpec is null)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.MissingSpecFile, Location.None, assemblyName));
                    return;
                }

                var (mutable, envelope) = MutableEmitter.Emit(authSpec, requestSpec);
                EmitAndAddSource(spc, mutable);
                EmitAndAddSource(spc, envelope);
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
