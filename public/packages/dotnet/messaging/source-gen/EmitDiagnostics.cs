// -----------------------------------------------------------------------
// <copyright file="EmitDiagnostics.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using DcsvIo.D2.SourceGen;

/// <summary>
/// Topic-specific factory helpers that produce
/// <see cref="EmitDiagnostic"/> instances with messaging-source-gen
/// descriptor IDs (<c>D2MQ*</c>). The diagnostic record itself lives in
/// <c>DcsvIo.D2.SourceGen</c> (shared across every source generator); only
/// the per-topic factory shape lives here.
/// </summary>
internal static class EmitDiagnostics
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
