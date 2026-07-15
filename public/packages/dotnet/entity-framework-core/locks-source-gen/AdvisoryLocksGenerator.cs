// -----------------------------------------------------------------------
// <copyright file="AdvisoryLocksGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.AdvisoryLocks.SourceGen;

using System;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>AdvisoryLocks</c>
/// static class into the owning-module assembly (currently
/// <c>DcsvIo.D2.Private.Edge.KeyCustodian.Infra</c>). Single-target dispatch — gates emission
/// on the consuming assembly name. Shared Postgres owns mechanism only
/// (<c>PgAdvisoryLock</c> / migrator); domain lock-key catalogs live with the
/// owning module. When a second database gains locks, upgrade to multi-target
/// or a per-destination MSBuild filter so foreign nests never ship on the
/// wrong assembly (uniqueness still validates the full central catalog).
/// </summary>
[Generator]
public sealed class AdvisoryLocksGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "AdvisoryLocks.g.cs";
    private const string _SPEC_FILE_NAME = "advisory-locks.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Private.Edge.KeyCustodian.Infra";

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

            var loadResult = AdvisoryLocksSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var emitResult = AdvisoryLocksEmitter.Emit(loadResult.Spec!);
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
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.DuplicateConstNameInDatabase =>
            DiagnosticDescriptors.DuplicateConstNameInDatabase,
        DiagnosticIds.DuplicateKeyInDatabase => DiagnosticDescriptors.DuplicateKeyInDatabase,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.KeyOutOfRange => DiagnosticDescriptors.KeyOutOfRange,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
