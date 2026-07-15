// -----------------------------------------------------------------------
// <copyright file="AudiencesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits audience catalogs from
/// <c>contracts/auth-audiences/audiences.spec.json</c> via <c>AdditionalFiles</c>.
/// Dual-target: public assembly <c>DcsvIo.D2.Auth.Abstractions</c> → <c>Audiences</c>
/// (public values only); private Extensions assembly
/// <c>DcsvIo.D2.Private.Auth.Abstractions.Extensions</c> → <c>ProductAudiences</c>
/// (public∪private values, distinct FQN under <c>DcsvIo.D2.Private.Auth</c>).
/// </summary>
[Generator]
public sealed class AudiencesGenerator : IIncrementalGenerator
{
    private const string _GENERATED_SOURCE_NAME = "Audiences.g.cs";
    private const string _PRIVATE_GENERATED_SOURCE_NAME = "ProductAudiences.g.cs";
    private const string _SPEC_FILE_NAME = "audiences.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Auth.Abstractions";
    private const string _PRIVATE_TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Private.Auth.Abstractions.Extensions";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.Auth.Abstractions";
    private const string _PRIVATE_ROOT_NAMESPACE = "DcsvIo.D2.Private.Auth";
    private const string _PUBLIC_CLASS_NAME = "Audiences";
    private const string _PRIVATE_CLASS_NAME = "ProductAudiences";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to audiences.spec.json files.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission by assembly name.
        var combined = specs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, drive the emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            var isPublic = string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal);
            var isPrivate = string.Equals(
                compilation.AssemblyName,
                _PRIVATE_TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal);

            if (!isPublic && !isPrivate)
            {
                return;
            }

            var rootNamespace = isPrivate ? _PRIVATE_ROOT_NAMESPACE : _PUBLIC_ROOT_NAMESPACE;
            var className = isPrivate ? _PRIVATE_CLASS_NAME : _PUBLIC_CLASS_NAME;
            var sourceName = isPrivate ? _PRIVATE_GENERATED_SOURCE_NAME : _GENERATED_SOURCE_NAME;

            if (specFiles.IsDefaultOrEmpty)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(EmitDiagnostics.MissingSpecFile()));
                spc.AddSource(
                    sourceName,
                    SourceText.From(EmptyAudiencesSource(rootNamespace, className), System.Text.Encoding.UTF8));
                return;
            }

            var ordered = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
            var loadResult = AudienceSpecLoader.Load(ordered[0].Path, ordered[0].Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                spc.AddSource(
                    sourceName,
                    SourceText.From(EmptyAudiencesSource(rootNamespace, className), System.Text.Encoding.UTF8));
                return;
            }

            var merged = loadResult.Spec!.Audiences.ToBuilder();
            var seen = new HashSet<string>(
                loadResult.Spec.Audiences.Select(a => a.Name),
                StringComparer.Ordinal);

            for (var i = 1; i < ordered.Count; i++)
            {
                var next = AudienceSpecLoader.Load(ordered[i].Path, ordered[i].Content);
                if (next.Diagnostic is { } nextDiag)
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(nextDiag));
                    continue;
                }

                foreach (var audience in next.Spec!.Audiences)
                {
                    if (!seen.Add(audience.Name))
                    {
                        continue;
                    }

                    merged.Add(audience);
                }
            }

            var emitResult = AudiencesEmitter.Emit(
                new AudiencesSpec(merged.ToImmutable()), rootNamespace, className);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                sourceName,
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
        DiagnosticIds.InvalidAudienceName => DiagnosticDescriptors.InvalidAudienceName,
        DiagnosticIds.DuplicateAudienceName => DiagnosticDescriptors.DuplicateAudienceName,
        DiagnosticIds.DuplicateAudienceUrl => DiagnosticDescriptors.DuplicateAudienceUrl,
        DiagnosticIds.InvalidAudienceUrl => DiagnosticDescriptors.InvalidAudienceUrl,
        DiagnosticIds.MissingSpecFile => DiagnosticDescriptors.MissingSpecFile,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private static string EmptyAudiencesSource(string rootNamespace, string className) =>
        "// <auto-generated>\n#nullable enable\n" +
        $"namespace {rootNamespace};\npublic static partial class {className} {{ }}\n";
}
