// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostic.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Messaging.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// A diagnostic produced by a loader or the emitter. Decoupled from
/// <c>Microsoft.CodeAnalysis.Diagnostic</c> so the loader and emitter are
/// unit-testable without instantiating a Roslyn host. <c>MqGenerator</c>
/// translates these into real Roslyn diagnostics.
/// </summary>
internal sealed record EmitDiagnostic(string DescriptorId, ImmutableArray<object> Args)
{
    public static EmitDiagnostic MalformedSpec(string fileName, string reason) =>
        new(DiagnosticIds.MalformedSpec, [fileName, reason]);

    public static EmitDiagnostic MissingRequiredField(
        string fileName, string entryConstantOrIndex, string fieldName) =>
        new(DiagnosticIds.MissingRequiredField, [fileName, entryConstantOrIndex, fieldName]);

    public static EmitDiagnostic DuplicateConstant(string fileName, string constantName) =>
        new(DiagnosticIds.DuplicateConstant, [fileName, constantName]);

    public static EmitDiagnostic UnknownEncryption(
        string constantName, string value, string validValues) =>
        new(DiagnosticIds.UnknownEncryption, [constantName, value, validValues]);

    public static EmitDiagnostic MissingPlaintextReason(string constantName) =>
        new(DiagnosticIds.MissingPlaintextReason, [constantName]);

    public static EmitDiagnostic UnknownPattern(
        string constantName, string value, string validValues) =>
        new(DiagnosticIds.UnknownPattern, [constantName, value, validValues]);

    public static EmitDiagnostic UnknownMessageType(
        string subscriptionConstant, string messageType) =>
        new(DiagnosticIds.UnknownMessageType, [subscriptionConstant, messageType]);

    public static EmitDiagnostic UnknownExchangeType(
        string constantName, string value) =>
        new(DiagnosticIds.UnknownExchangeType, [constantName, value]);

    public static EmitDiagnostic MissingMessagesSpecFile() =>
        new(DiagnosticIds.MissingMessagesSpecFile, []);

    public static EmitDiagnostic MissingSubscriptionsSpecFile() =>
        new(DiagnosticIds.MissingSubscriptionsSpecFile, []);

    public static EmitDiagnostic InvalidConstantName(
        string fileName, string value, string reason) =>
        new(DiagnosticIds.InvalidConstantName, [fileName, value, reason]);

    public static EmitDiagnostic InvalidTieredRetryDuration(
        string subscriptionConstant, string value) =>
        new(DiagnosticIds.InvalidTieredRetryDuration, [subscriptionConstant, value]);
}
