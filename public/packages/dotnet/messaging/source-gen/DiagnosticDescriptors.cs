// -----------------------------------------------------------------------
// <copyright file="DiagnosticDescriptors.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using Microsoft.CodeAnalysis;

/// <summary>
/// Roslyn <see cref="DiagnosticDescriptor"/> instances for the IDs in
/// <see cref="DiagnosticIds"/>. Lives in the Roslyn-host-aware seam so the
/// pure-logic loader / emitter stays decoupled from
/// <c>Microsoft.CodeAnalysis</c>.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string _CATEGORY = "D2.Mq";

    public static readonly DiagnosticDescriptor MalformedSpec = new(
        id: DiagnosticIds.MalformedSpec,
        title: "Malformed messaging spec",
        messageFormat: "Spec file '{0}' is malformed: {1}",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingRequiredField = new(
        id: DiagnosticIds.MissingRequiredField,
        title: "Required field missing on messaging spec entry",
        messageFormat: "Spec file '{0}': entry '{1}' missing required field '{2}'",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateConstant = new(
        id: DiagnosticIds.DuplicateConstant,
        title: "Duplicate constant in messaging spec",
        messageFormat: "Spec file '{0}': constant '{1}' declared more than once",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownEncryption = new(
        id: DiagnosticIds.UnknownEncryption,
        title: "Unknown encryption value",
        messageFormat:
            "Message '{0}': encryption value '{1}' is not recognized "
            + "(must be one of {2}, or 'plaintext' with an encryptionReason)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingPlaintextReason = new(
        id: DiagnosticIds.MissingPlaintextReason,
        title: "Plaintext message missing encryptionReason",
        messageFormat:
            "Message '{0}' has encryption=plaintext but no encryptionReason. "
            + "Plaintext bypasses payload confidentiality — provide a written "
            + "rationale (audit log + code review) per spec.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownPattern = new(
        id: DiagnosticIds.UnknownPattern,
        title: "Unknown subscription pattern",
        messageFormat:
            "Subscription '{0}': pattern '{1}' is not recognized "
            + "(must be one of {2})",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownMessageType = new(
        id: DiagnosticIds.UnknownMessageType,
        title: "Subscription references unknown message type",
        messageFormat:
            "Subscription '{0}': messageType '{1}' is not declared in "
            + "mq-messages.spec.json",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownExchangeType = new(
        id: DiagnosticIds.UnknownExchangeType,
        title: "Unknown exchange type",
        messageFormat:
            "Message '{0}': exchangeType '{1}' is not recognized "
            + "(must be one of fanout, topic, direct)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingMessagesSpecFile = new(
        id: DiagnosticIds.MissingMessagesSpecFile,
        title: "Missing mq-messages.spec.json",
        messageFormat:
            "Target assembly references the Mq source-gen but no "
            + "mq-messages.spec.json was found in <AdditionalFiles>. "
            + "Either include the file or remove the analyzer reference.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingSubscriptionsSpecFile = new(
        id: DiagnosticIds.MissingSubscriptionsSpecFile,
        title: "Missing mq-subscriptions.spec.json",
        messageFormat:
            "Target assembly references the Mq source-gen but no "
            + "mq-subscriptions.spec.json was found in <AdditionalFiles>. "
            + "Either include the file or remove the analyzer reference.",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidConstantName = new(
        id: DiagnosticIds.InvalidConstantName,
        title: "Invalid constant name in messaging spec",
        messageFormat:
            "Spec file '{0}': constant '{1}' is invalid: {2} "
            + "(must start with uppercase letter, contain only ASCII letters / digits)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidTieredRetryDuration = new(
        id: DiagnosticIds.InvalidTieredRetryDuration,
        title: "Invalid tiered-retry duration",
        messageFormat:
            "Subscription '{0}': tieredRetry tier value '{1}' is not a "
            + "valid TimeSpan (HH:MM:SS)",
        category: _CATEGORY,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
