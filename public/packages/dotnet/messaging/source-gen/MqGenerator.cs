// -----------------------------------------------------------------------
// <copyright file="MqGenerator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DcsvIo.D2.SourceGen;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

/// <summary>
/// Roslyn incremental source generator that emits the messaging registry
/// types (<c>MqMessages</c> + <c>MqMessagesRegistry</c> + <c>MqSubscriptions</c>
/// + <c>MqSubscriptionsRegistry</c>) into <c>DcsvIo.D2.Messaging.Abstractions</c>
/// from the two spec files. Single-target — only emits when the consuming
/// assembly is <c>DcsvIo.D2.Messaging.Abstractions</c>.
/// </summary>
[Generator]
public sealed class MqGenerator : IIncrementalGenerator
{
    private const string _MESSAGES_HINT = "MqMessages.g.cs";
    private const string _SUBSCRIPTIONS_HINT = "MqSubscriptions.g.cs";
    private const string _MESSAGES_FILE = "mq-messages.spec.json";
    private const string _SUBSCRIPTIONS_FILE = "mq-subscriptions.spec.json";
    private const string _TARGET_ASSEMBLY = "DcsvIo.D2.Messaging.Abstractions";
    private const string _ENCRYPTION_DOMAINS_FQN = "DcsvIo.D2.Encryption.EncryptionDomains";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var specs = context.AdditionalTextsProvider
            .Where(static file => IsMessagesSpec(file.Path) || IsSubscriptionsSpec(file.Path))
            .Select(static (file, ct) => new SpecFile(
                Path: file.Path,
                Content: file.GetText(ct)?.ToString() ?? string.Empty))
            .Collect();

        var combined = specs.Combine(context.CompilationProvider);

        context.RegisterSourceOutput(combined, static (spc, tuple) =>
        {
            var (specFiles, compilation) = tuple;

            if (!string.Equals(
                compilation.AssemblyName, _TARGET_ASSEMBLY, StringComparison.Ordinal))
            {
                return;
            }

            // Find the single messages spec + the single subscriptions spec.
            var messagesFile = specFiles
                .Where(s => IsMessagesSpec(s.Path))
                .OrderBy(s => s.Path, StringComparer.Ordinal)
                .FirstOrDefault();
            var subsFile = specFiles
                .Where(s => IsSubscriptionsSpec(s.Path))
                .OrderBy(s => s.Path, StringComparer.Ordinal)
                .FirstOrDefault();

            if (messagesFile is null)
            {
                spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.MissingMessagesSpecFile()));
                EmitEmpty(spc);
                return;
            }

            if (subsFile is null)
            {
                spc.ReportDiagnostic(ToRoslyn(EmitDiagnostics.MissingSubscriptionsSpecFile()));
                EmitEmpty(spc);
                return;
            }

            var messagesLoad = MqMessagesLoader.Load(messagesFile.Path, messagesFile.Content);
            if (messagesLoad.Diagnostic is { } mDiag)
            {
                spc.ReportDiagnostic(ToRoslyn(mDiag));
                EmitEmpty(spc);
                return;
            }

            var subsLoad = MqSubscriptionsLoader.Load(subsFile.Path, subsFile.Content);
            if (subsLoad.Diagnostic is { } sDiag)
            {
                spc.ReportDiagnostic(ToRoslyn(sDiag));
                EmitEmpty(spc);
                return;
            }

            var encryptionDomains = ExtractConstStringValues(compilation, _ENCRYPTION_DOMAINS_FQN);

            var (messagesEmit, subsEmit) = MqEmitter.Emit(
                messagesLoad.Spec!, subsLoad.Spec!, encryptionDomains);

            foreach (var d in messagesEmit.Diagnostics) spc.ReportDiagnostic(ToRoslyn(d));
            foreach (var d in subsEmit.Diagnostics) spc.ReportDiagnostic(ToRoslyn(d));

            spc.AddSource(
                _MESSAGES_HINT,
                SourceText.From(messagesEmit.GeneratedSource, System.Text.Encoding.UTF8));
            spc.AddSource(
                _SUBSCRIPTIONS_HINT,
                SourceText.From(subsEmit.GeneratedSource, System.Text.Encoding.UTF8));
        });
    }

    private static IReadOnlyList<string> ExtractConstStringValues(
        Compilation compilation, string fullyQualifiedName)
    {
        var symbol = compilation.GetTypeByMetadataName(fullyQualifiedName);
        if (symbol is null) return [];

        var values = new List<string>();
        foreach (var member in symbol.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true, ConstantValue: string v })
                values.Add(v);
        }

        return values;
    }

    private static bool IsMessagesSpec(string path) =>
        string.Equals(Path.GetFileName(path), _MESSAGES_FILE, StringComparison.OrdinalIgnoreCase);

    private static bool IsSubscriptionsSpec(string path) =>
        string.Equals(
            Path.GetFileName(path), _SUBSCRIPTIONS_FILE, StringComparison.OrdinalIgnoreCase);

    private static void EmitEmpty(SourceProductionContext spc)
    {
        var empty =
            "// <auto-generated>\n#nullable enable\n"
            + "namespace DcsvIo.D2.Messaging;\n";
        spc.AddSource(_MESSAGES_HINT, SourceText.From(empty, System.Text.Encoding.UTF8));
        spc.AddSource(_SUBSCRIPTIONS_HINT, SourceText.From(empty, System.Text.Encoding.UTF8));
    }

    private static Diagnostic ToRoslyn(EmitDiagnostic d)
    {
        var descriptor = ResolveDescriptor(d.DescriptorId);
        return Diagnostic.Create(descriptor, Location.None, d.Args.ToArray());
    }

    private static DiagnosticDescriptor ResolveDescriptor(string id) => id switch
    {
        DiagnosticIds.MalformedSpec => DiagnosticDescriptors.MalformedSpec,
        DiagnosticIds.MissingRequiredField => DiagnosticDescriptors.MissingRequiredField,
        DiagnosticIds.DuplicateConstant => DiagnosticDescriptors.DuplicateConstant,
        DiagnosticIds.UnknownEncryption => DiagnosticDescriptors.UnknownEncryption,
        DiagnosticIds.MissingPlaintextReason => DiagnosticDescriptors.MissingPlaintextReason,
        DiagnosticIds.UnknownPattern => DiagnosticDescriptors.UnknownPattern,
        DiagnosticIds.UnknownMessageType => DiagnosticDescriptors.UnknownMessageType,
        DiagnosticIds.UnknownExchangeType => DiagnosticDescriptors.UnknownExchangeType,
        DiagnosticIds.MissingMessagesSpecFile => DiagnosticDescriptors.MissingMessagesSpecFile,
        DiagnosticIds.MissingSubscriptionsSpecFile =>
            DiagnosticDescriptors.MissingSubscriptionsSpecFile,
        DiagnosticIds.InvalidConstantName => DiagnosticDescriptors.InvalidConstantName,
        DiagnosticIds.InvalidTieredRetryDuration =>
            DiagnosticDescriptors.InvalidTieredRetryDuration,
        _ => throw new InvalidOperationException($"Unknown EmitDiagnostic descriptor id '{id}'."),
    };
}
