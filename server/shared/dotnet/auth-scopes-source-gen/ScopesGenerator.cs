// -----------------------------------------------------------------------
// <copyright file="ScopesGenerator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Auth.Scopes.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the <c>Scopes</c> static
/// partial class into <c>D2.Shared.Auth.Abstractions</c> by reading
/// <c>contracts/auth-scopes/scopes.spec.json</c> via <c>AdditionalFiles</c>.
/// Single-target — only emits when the consuming assembly is
/// <c>D2.Shared.Auth.Abstractions</c>.
/// </summary>
[Generator]
public sealed class ScopesGenerator : IIncrementalGenerator
{
    private const string _GENERATED_SOURCE_NAME = "Scopes.g.cs";
    private const string _SPEC_FILE_NAME = "scopes.spec.json";
    private const string _TARGET_ASSEMBLY_NAME = "D2.Shared.Auth.Abstractions";
    private const string _ORG_TYPE_FQN = "D2.Shared.Auth.Abstractions.OrgType";
    private const string _ROLE_FQN = "D2.Shared.Auth.Abstractions.Role";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. Filter AdditionalFiles to scopes.spec.json files.
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsSpecFile(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        // 2. Combine with the compilation so we can gate emission by assembly name
        //    AND extract OrgType / Role enum members at codegen time (so the
        //    grantedTo wildcard expansion picks up new members automatically).
        var combined = specs.Combine(context.CompilationProvider);

        // 3. For each pipeline run, drive the emitter.
        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            // Only emit when the consuming assembly is D2.Shared.Auth.Abstractions.
            if (!string.Equals(
                compilation.AssemblyName,
                _TARGET_ASSEMBLY_NAME,
                StringComparison.Ordinal))
                return;

            if (specFiles.IsDefaultOrEmpty)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.MissingSpecFile,
                    Location.None));
                spc.AddSource(
                    _GENERATED_SOURCE_NAME,
                    SourceText.From(EmptyScopesSource(), System.Text.Encoding.UTF8));
                return;
            }

            // Convention: only one scopes.spec.json per consuming project.
            var spec = specFiles.OrderBy(s => s.Path, StringComparer.Ordinal).First();

            var loadResult = ScopeSpecLoader.Load(spec.Path, spec.Content);
            if (loadResult.Diagnostic is { } loadDiag)
            {
                spc.ReportDiagnostic(ToRoslynDiagnostic(loadDiag));
                spc.AddSource(
                    _GENERATED_SOURCE_NAME,
                    SourceText.From(EmptyScopesSource(), System.Text.Encoding.UTF8));
                return;
            }

            // Extract OrgType + Role enum members from the compilation. These
            // drive grantedTo validation + wildcard expansion inside the
            // emitter, so adding a new enum member is picked up automatically
            // without touching the SrcGen.
            var orgTypeNames = ExtractEnumMembers(compilation, _ORG_TYPE_FQN);
            var roleNames = ExtractEnumMembers(compilation, _ROLE_FQN);

            var emitResult = ScopesEmitter.Emit(loadResult.Spec!, orgTypeNames, roleNames);
            foreach (var d in emitResult.Diagnostics)
                spc.ReportDiagnostic(ToRoslynDiagnostic(d));

            spc.AddSource(
                _GENERATED_SOURCE_NAME,
                SourceText.From(emitResult.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    /// <summary>
    /// Reads enum member names from the compilation by fully-qualified type
    /// name. Returns the member names in source order. Returns an empty list
    /// when the type isn't in the compilation (e.g. mid-build before
    /// auth-abstractions is fully resolved).
    /// </summary>
    private static IReadOnlyList<string> ExtractEnumMembers(
        Compilation compilation,
        string fullyQualifiedName)
    {
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
        if (symbol is not { TypeKind: TypeKind.Enum })
            return [];

        var members = new List<string>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, IsStatic: true } field)
                members.Add(field.Name);
        }

        return members;
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
        DiagnosticIds.UnknownEnumValue => DiagnosticDescriptors.UnknownEnumValue,
        DiagnosticIds.InvalidScopeName => DiagnosticDescriptors.InvalidScopeName,
        DiagnosticIds.DuplicateScope => DiagnosticDescriptors.DuplicateScope,
        DiagnosticIds.AnonImpersonationBlockedNoise =>
            DiagnosticDescriptors.AnonImpersonationBlockedNoise,
        DiagnosticIds.EmptyRoleArray => DiagnosticDescriptors.EmptyRoleArray,
        DiagnosticIds.TreePositionCollision => DiagnosticDescriptors.TreePositionCollision,
        DiagnosticIds.MissingGrantedTo => DiagnosticDescriptors.MissingGrantedTo,
        DiagnosticIds.MissingSpecFile => DiagnosticDescriptors.MissingSpecFile,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };

    private static string EmptyScopesSource() =>
        "// <auto-generated>\n#nullable enable\n" +
        "namespace D2.Shared.Auth.Abstractions;\npublic static partial class Scopes { }\n";
}
