// -----------------------------------------------------------------------
// <copyright file="EncryptionDomainsGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits encryption-domain catalogs.
/// Dual-target: public assembly <c>DcsvIo.D2.Encryption</c> → <c>EncryptionDomains</c>
/// (public values only); private Extensions assembly
/// <c>DcsvIo.D2.Private.Encryption.Extensions</c> → <c>ProductEncryptionDomains</c>
/// (public∪private values, distinct FQN under <c>DcsvIo.D2.Private.Encryption</c>).
/// </summary>
[Generator]
public sealed class EncryptionDomainsGenerator : IIncrementalGenerator
{
    private const string _SOURCE_NAME = "EncryptionDomains.g.cs";
    private const string _PRIVATE_SOURCE_NAME = "ProductEncryptionDomains.g.cs";
    private const string _SPEC_FILE_NAME = "encryption-domains.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Encryption";
    private const string _PRIVATE_TARGET_ASSEMBLY_NAME = "DcsvIo.D2.Private.Encryption.Extensions";
    private const string _PUBLIC_ROOT_NAMESPACE = "DcsvIo.D2.Encryption";
    private const string _PRIVATE_ROOT_NAMESPACE = "DcsvIo.D2.Private.Encryption";
    private const string _PUBLIC_CLASS_NAME = "EncryptionDomains";
    private const string _PRIVATE_CLASS_NAME = "ProductEncryptionDomains";
    private const string _PUBLIC_MODE_ENUM = "EncryptionDomainMode";
    private const string _PRIVATE_MODE_ENUM = "ProductEncryptionDomainMode";
    private const string _PUBLIC_MODES_CLASS = "EncryptionDomainModes";
    private const string _PRIVATE_MODES_CLASS = "ProductEncryptionDomainModes";

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

            var isPublic = string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal);
            var isPrivate = string.Equals(
                compilation.AssemblyName,
                _PRIVATE_TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal);

            if (!isPublic && !isPrivate)
                return;

            if (specFiles.IsDefaultOrEmpty)
                return;

            var rootNamespace = isPrivate ? _PRIVATE_ROOT_NAMESPACE : _PUBLIC_ROOT_NAMESPACE;
            var className = isPrivate ? _PRIVATE_CLASS_NAME : _PUBLIC_CLASS_NAME;
            var modeEnum = isPrivate ? _PRIVATE_MODE_ENUM : _PUBLIC_MODE_ENUM;
            var modesClass = isPrivate ? _PRIVATE_MODES_CLASS : _PUBLIC_MODES_CLASS;
            var sourceName = isPrivate ? _PRIVATE_SOURCE_NAME : _SOURCE_NAME;

            var ordered = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).ToList();
            var loadResult = EncryptionDomainsSpecLoader.Load(ordered[0].Path, ordered[0].Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                return;
            }

            var mergedDomains = loadResult.Spec!.Domains.ToBuilder();
            var seen = new HashSet<string>(
                loadResult.Spec.Domains.Select(d => d.ConstName),
                StringComparer.Ordinal);

            for (var i = 1; i < ordered.Count; i++)
            {
                var next = EncryptionDomainsSpecLoader.Load(ordered[i].Path, ordered[i].Content);
                if (next.Diagnostic is { } nextDiag)
                {
                    spc.ReportDiagnostic(ToRoslynDiagnostic(nextDiag));
                    continue;
                }

                foreach (var domain in next.Spec!.Domains)
                {
                    if (!seen.Add(domain.ConstName))
                        continue;

                    mergedDomains.Add(domain);
                }
            }

            var emitResult = EncryptionDomainsEmitter.Emit(
                new EncryptionDomainsSpec(mergedDomains.ToImmutable()),
                rootNamespace,
                className,
                modeEnum,
                modesClass);
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
        DiagnosticIds.DuplicateConstName => DiagnosticDescriptors.DuplicateConstName,
        DiagnosticIds.DuplicateValue => DiagnosticDescriptors.DuplicateValue,
        DiagnosticIds.InvalidConstName => DiagnosticDescriptors.InvalidConstName,
        DiagnosticIds.EmptyValue => DiagnosticDescriptors.EmptyValue,
        DiagnosticIds.InvalidMode => DiagnosticDescriptors.InvalidMode,
        DiagnosticIds.MissingConsumerService =>
            DiagnosticDescriptors.MissingConsumerService,
        DiagnosticIds.UnexpectedConsumerService =>
            DiagnosticDescriptors.UnexpectedConsumerService,
        DiagnosticIds.InvalidConsumerService =>
            DiagnosticDescriptors.InvalidConsumerService,
        _ => throw new InvalidOperationException(
            $"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
