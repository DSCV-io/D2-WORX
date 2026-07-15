// -----------------------------------------------------------------------
// <copyright file="ErrorCodesEngine.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// The unified error-codes generation engine. ONE engine drives every
/// <c>*-error-codes</c> catalog; each consuming assembly wires a thin
/// <c>[Generator]</c> shell that calls <see cref="Run"/> with its own
/// <see cref="CatalogConfig"/>. The shell keeps its assembly identity + the
/// <c>ErrorCodesGenerator</c> type FQN (both load-bearing for the on-disk
/// <c>Generated/{assembly}/{typeFQN}/</c> output path), while this engine owns
/// all the catalog-agnostic logic: spec loading, constants + failures
/// emission, the domain-prefix diagnostic (<c>D2ERC001</c>), and the
/// userMessageKey → en-US.json TK-existence cross-check (<c>D2ERC002</c>).
/// </summary>
internal static class ErrorCodesEngine
{
    private const string _MESSAGES_DIRECTORY_NAME = "messages";
    private const string _EN_US_FILE_NAME = "en-US.json";
    private const string _CATEGORY_SPEC_FILE_NAME = "error-category.spec.json";

    /// <summary>
    /// Registers the engine's incremental pipeline for one catalog. Surfaces
    /// the catalog spec + (when factory-bearing) en-US.json via
    /// <c>AdditionalFiles</c>, gates emission on the consuming assembly name,
    /// and emits the constants file (+ the failures file when
    /// <see cref="CatalogConfig.EmitFailures"/> is set).
    /// </summary>
    /// <param name="context">The generator initialization context.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <param name="resolvePerCatalogDescriptor">
    /// Maps a per-catalog diagnostic id (<c>D2EC*</c> / <c>D2AEC*</c>) to its
    /// Roslyn descriptor. The shell owns these so the pre-existing
    /// per-catalog diagnostic-id families do not churn; the engine resolves
    /// its own <c>D2ERC*</c> ids before delegating to this.
    /// </param>
    public static void Run(
        IncrementalGeneratorInitializationContext context,
        CatalogConfig config,
        Func<string, DiagnosticDescriptor> resolvePerCatalogDescriptor)
    {
        // 1. Surface this catalog's spec file via AdditionalFiles.
        var specs = context.AdditionalTextsProvider
            .Where(file => IsSpecFile(file.Path, config.SpecFileName))
            .Select((file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Reduce en-US.json to a value-equatable key set at the earliest
        //    boundary so a translation-VALUE edit does not invalidate the
        //    cache — only a key ADD/REMOVE re-runs the TK cross-check.
        var messageKeys = context.AdditionalTextsProvider
            .Where(static file => IsEnUsMessages(file.Path))
            .Select(static (file, ct) => MessageKeySet.Parse(
                file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 3. Surface error-category.spec.json so the category-membership check
        //    validates against the spec-derived closed set rather than a
        //    hand-maintained subset. Absent / malformed → empty set → the check
        //    degrades to a no-op (same as the en-US.json TK cross-check).
        var categorySpecs = context.AdditionalTextsProvider
            .Where(static file => IsCategorySpec(file.Path))
            .Select(static (file, ct) => file.GetText(ct)?.ToString() ?? string.Empty)
            .Collect();

        var combined = specs
            .Combine(context.CompilationProvider)
            .Combine(messageKeys)
            .Combine(categorySpecs);

        context.RegisterSourceOutput(combined, (spc, tuple) =>
        {
            var (((specFiles, compilation), keySets), categorySpecContents) = tuple;

            if (!string.Equals(
                compilation.AssemblyName,
                config.TargetAssemblyName,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            // Convention: one spec of this filename per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            DiagnosticDescriptor Resolve(string id) => ResolveDescriptor(
                id, resolvePerCatalogDescriptor);

            var loadResult = ErrorCodeSpecLoader.Load(
                spec.Path, spec.Content, config.MalformedSpecId);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag, Resolve));
                return;
            }

            var parsedSpec = loadResult.Spec!;

            // The spec-derived closed category set the membership check
            // validates against. Empty when error-category.spec.json was not
            // surfaced / was unparseable — the check then degrades to a no-op.
            var categoryWireSet = categorySpecContents.IsDefaultOrEmpty
                ? ImmutableHashSet<string>.Empty.WithComparer(StringComparer.Ordinal)
                : CategoryWireSetLoader.LoadWireSet(
                    categorySpecContents.OrderBy(c => c, StringComparer.Ordinal).First());

            var codesResult = ConstantsEmitter.Emit(parsedSpec, config, categoryWireSet);
            foreach (var d in codesResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d, Resolve));

            // Engine-level TK-existence cross-check (factory-bearing catalogs
            // only — the generic constants-only catalog has no userMessageKey
            // and no en-US.json AdditionalFile).
            if (config.EmitFailures)
            {
                // Dual message roots: merge every en-US.json AdditionalFile key set
                // (public∪private). First-file-only would drop private product keys.
                var keySet = keySets.IsDefaultOrEmpty
                    ? MessageKeySet.Empty
                    : MessageKeySet.Union(keySets);
                foreach (var diag in TkExistenceDiagnostics(parsedSpec, keySet))
                    spc.ReportDiagnostic(ToRoslynDiagnostic(diag, Resolve));
            }

            spc.AddSource(
                config.ConstantsSourceName,
                SourceText.From(codesResult.GeneratedSource, System.Text.Encoding.UTF8));

            if (config.EmitFailures)
                EmitFailureSources(spc, parsedSpec, config, Resolve);
        });
    }

    /// <summary>
    /// Emits the catalog's failure sources per its <see cref="FactoryHost"/>:
    /// the generic constructing catalog (<see cref="FactoryHost.Base"/>) emits
    /// the factories + <c>&lt;TData&gt;</c> twins + per-code booleans onto the
    /// <c>D2Result</c> partials; a per-domain catalog
    /// (<see cref="FactoryHost.Domain"/>) emits the delegating
    /// <c>&lt;Domain&gt;Failures</c> + <c>&lt;Domain&gt;Failures&lt;T&gt;</c>
    /// classes.
    /// </summary>
    private static void EmitFailureSources(
        SourceProductionContext spc,
        ErrorCodesSpec spec,
        CatalogConfig config,
        Func<string, DiagnosticDescriptor> resolve)
    {
        if (config.FactoryHost == FactoryHost.Base)
        {
            var factories = BaseFactoriesEmitter.EmitFactories(spec, config);
            Add(spc, config.BaseFactoriesSourceName!, factories.GeneratedSource);

            var genericFactories = BaseFactoriesEmitter.EmitGenericFactories(spec, config);
            Add(spc, config.BaseGenericFactoriesSourceName!, genericFactories.GeneratedSource);

            var booleans = BaseFactoriesEmitter.EmitBooleans(spec, config);
            Add(spc, config.BaseBooleansSourceName!, booleans.GeneratedSource);
            return;
        }

        // FactoryHost.Domain — the delegating non-generic + generic classes.
        var failures = FailuresEmitter.Emit(spec, config);
        foreach (var d in failures.Diagnostics)
            spc.ReportDiagnostic(ToRoslynDiagnostic(d, resolve));

        Add(spc, config.FailuresSourceName!, failures.GeneratedSource);

        var genericFailures = FailuresEmitter.EmitGeneric(spec, config);
        Add(spc, config.GenericFailuresSourceName!, genericFailures.GeneratedSource);
    }

    private static void Add(SourceProductionContext spc, string hintName, string source) =>
        spc.AddSource(hintName, SourceText.From(source, System.Text.Encoding.UTF8));

    private static IEnumerable<EmitDiagnostic> TkExistenceDiagnostics(
        ErrorCodesSpec spec, MessageKeySet keySet)
    {
        // No key set means en-US.json was not surfaced / was unparseable —
        // skip the cross-check rather than fire false positives.
        if (keySet.IsEmpty)
            yield break;

        foreach (var entry in spec.ErrorCodes)
        {
            if (entry.UserMessageKey is not { } key)
                continue;

            var snakeKey = TkKeyTransform.ToSnakeKey(key);
            if (snakeKey is null || !keySet.Contains(snakeKey))
            {
                yield return EngineDiagnostics.TkKeyNotFound(
                    entry.Code, key, snakeKey ?? key);
            }
        }
    }

    private static bool IsSpecFile(string path, string specFileName) =>
        string.Equals(
            Path.GetFileName(path),
            specFileName,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsCategorySpec(string path) =>
        string.Equals(
            Path.GetFileName(path),
            _CATEGORY_SPEC_FILE_NAME,
            StringComparison.OrdinalIgnoreCase);

    private static bool IsEnUsMessages(string path)
    {
        if (!string.Equals(
            Path.GetFileName(path),
            _EN_US_FILE_NAME,
            StringComparison.OrdinalIgnoreCase))
            return false;

        var dir = Path.GetDirectoryName(path);
        if (dir is null)
            return false;

        return string.Equals(
            Path.GetFileName(dir),
            _MESSAGES_DIRECTORY_NAME,
            StringComparison.OrdinalIgnoreCase);
    }

    private static Diagnostic ToRoslynDiagnostic(
        EmitDiagnostic emitDiag, Func<string, DiagnosticDescriptor> resolve)
    {
        var descriptor = resolve(emitDiag.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, emitDiag.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(
        string id, Func<string, DiagnosticDescriptor> resolvePerCatalogDescriptor) => id switch
    {
        EngineDiagnosticIds.DomainPrefixViolation =>
            EngineDiagnosticDescriptors.DomainPrefixViolation,
        EngineDiagnosticIds.TkKeyNotFound =>
            EngineDiagnosticDescriptors.TkKeyNotFound,
        EngineDiagnosticIds.UnsupportedFactoryShape =>
            EngineDiagnosticDescriptors.UnsupportedFactoryShape,
        _ => resolvePerCatalogDescriptor(id),
    };
}
