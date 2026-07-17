// -----------------------------------------------------------------------
// <copyright file="AnonymizationEngineLog.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.EntityFrameworkCore;

using Microsoft.Extensions.Logging;

/// <summary>
/// PII-safe <c>LoggerMessage</c> delegates for the anonymization engine.
/// Subject ids are never logged — only sweep correlation ids, entity-type
/// names (model metadata), tier labels, row counts, and sanitized exception
/// type + first-frame strings appear here.
/// </summary>
internal static partial class AnonymizationEngineLog
{
    [LoggerMessage(
        EventId = 9400,
        Level = LogLevel.Debug,
        Message = "Anonymization sweep started (sweepId={SweepId}, ownershipKind={OwnershipKind}, "
            + "entityTypeCount={EntityTypeCount}).")]
    public static partial void SweepStarted(
        ILogger logger,
        string sweepId,
        string ownershipKind,
        int entityTypeCount);

    [LoggerMessage(
        EventId = 9401,
        Level = LogLevel.Debug,
        Message = "Anonymization entity-type done (sweepId={SweepId}, "
            + "entityTypeName={EntityTypeName}, tier={Tier}, "
            + "rowsAnonymized={RowsAnonymized}).")]
    public static partial void EntityTypeDone(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        string tier,
        int rowsAnonymized);

    [LoggerMessage(
        EventId = 9402,
        Level = LogLevel.Information,
        Message = "Anonymization sweep complete (sweepId={SweepId}, "
            + "entityTypesProcessed={EntityTypesProcessed}, "
            + "rowsAnonymized={RowsAnonymized}, "
            + "entityTypesSkippedExempt={EntityTypesSkippedExempt}, "
            + "alreadyAnonymizedRows={AlreadyAnonymizedRows}).")]
    public static partial void SweepComplete(
        ILogger logger,
        string sweepId,
        int entityTypesProcessed,
        int rowsAnonymized,
        int entityTypesSkippedExempt,
        int alreadyAnonymizedRows);

    [LoggerMessage(
        EventId = 9403,
        Level = LogLevel.Error,
        Message = "Anonymization sweep failed: Tier-C entity type reached runtime "
            + "— this is a startup-guard bypass "
            + "(sweepId={SweepId}, entityTypeName={EntityTypeName}, "
            + "blockerProperty={BlockerProperty}).")]
    public static partial void TierCReachedRuntime(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        string blockerProperty);

    [LoggerMessage(
        EventId = 9404,
        Level = LogLevel.Error,
        Message = "Anonymization sweep failed on entity type (sweepId={SweepId}, "
            + "entityTypeName={EntityTypeName}, exType={ExType}, firstFrame={FirstFrame}).")]
    public static partial void EntityTypeError(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        string exType,
        string firstFrame);

    [LoggerMessage(
        EventId = 9405,
        Level = LogLevel.Warning,
        Message = "Anonymization Tier-B concurrency conflict; retrying chunk "
            + "(sweepId={SweepId}, entityTypeName={EntityTypeName}, "
            + "attempt={Attempt}/{MaxAttempts}).")]
    public static partial void TierBConcurrencyRetry(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 9406,
        Level = LogLevel.Error,
        Message = "Anonymization Tier-B concurrency conflict exhausted retries; returning failure "
            + "(sweepId={SweepId}, entityTypeName={EntityTypeName}, maxAttempts={MaxAttempts}).")]
    public static partial void TierBConcurrencyExhausted(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        int maxAttempts);

    [LoggerMessage(
        EventId = 9407,
        Level = LogLevel.Error,
        Message = "Anonymization Tier-A plan misconfiguration "
            + "— SetNull on non-nullable value-type column; "
            + "returning failure (sweepId={SweepId}, "
            + "entityTypeName={EntityTypeName}, reason={Reason}).")]
    public static partial void TierASetNullMisconfiguration(
        ILogger logger,
        string sweepId,
        string entityTypeName,
        string reason);

    [LoggerMessage(
        EventId = 9408,
        Level = LogLevel.Error,
        Message = "Anonymization Tier-A plan could not be built for entity type "
            + "(sweepId={SweepId}, entityTypeName={EntityTypeName}). "
            + "The setter expression could not be constructed — returning UnhandledException.")]
    public static partial void TierAPlanInvalid(
        ILogger logger,
        string sweepId,
        string entityTypeName);
}
