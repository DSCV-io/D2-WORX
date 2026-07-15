// -----------------------------------------------------------------------
// <copyright file="IAnonymizationEngine.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

using DcsvIo.D2.Result;

/// <summary>
/// Engine seam — the single entry point for triggering a GDPR erasure sweep over all
/// registered entity types. The concrete implementation lives in
/// <c>DcsvIo.D2.DataGovernance.EntityFrameworkCore</c>; domain and host code
/// depend on this interface without pulling in EF Core.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Idempotency:</strong> the engine excludes rows whose
/// <see cref="IAnonymizationTrackable.IsAnonymized"/> flag is already
/// <see langword="true"/>. Calling the engine a second time for the same subject returns
/// <c>Ok</c> with <see cref="AnonymizationOutcome.AlreadyAnonymizedRows"/> reporting the
/// count of skipped rows and <see cref="AnonymizationOutcome.RowsAnonymized"/> at zero.
/// </para>
/// <para>
/// <strong>Validation:</strong> passing <see cref="Guid.Empty"/> as <c>userId</c> or
/// <c>orgId</c> returns <c>ValidationFailed</c> immediately — no database writes are
/// attempted.
/// </para>
/// <para>
/// <strong>Outcome counters:</strong>
/// <see cref="AnonymizationOutcome.EntityTypesProcessed"/> — entity types examined (not
/// exempt); <see cref="AnonymizationOutcome.RowsAnonymized"/> — rows actually overwritten;
/// <see cref="AnonymizationOutcome.EntityTypesSkippedExempt"/> — entity types skipped
/// because they implement <see cref="IExemptFromAnonymization"/>;
/// <see cref="AnonymizationOutcome.AlreadyAnonymizedRows"/> — rows skipped because
/// <see cref="IAnonymizationTrackable.IsAnonymized"/> was already <see langword="true"/>.
/// </para>
/// <para>
/// <strong>Why <c>Task</c> and not <c>ValueTask</c>:</strong> the engine always performs
/// real async I/O (EF Core <c>ExecuteUpdateAsync</c> / <c>SaveChangesAsync</c>) with no
/// synchronous fast path, so the single-await discipline of <c>ValueTask</c> provides no
/// benefit and adds a consumption footgun.
/// </para>
/// </remarks>
public interface IAnonymizationEngine
{
    /// <summary>
    /// Runs an anonymization sweep over all <see cref="IUserOwned"/>-marked entity types
    /// for the given user subject, overwriting PII fields according to their registered
    /// <see cref="AnonymizationRule"/>.
    /// </summary>
    /// <param name="userId">
    /// The id of the user whose data is to be erased. Passing <see cref="Guid.Empty"/>
    /// returns <c>D2Result.ValidationFailed</c> with no database writes.
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// <c>D2Result.Ok(<see cref="AnonymizationOutcome"/>)</c> on success (including the
    /// idempotent re-run case where all rows were already anonymized), or
    /// <c>D2Result.ValidationFailed</c> when <paramref name="userId"/> is
    /// <see cref="Guid.Empty"/>. Implementations may additionally return other
    /// <c>D2Result</c> failures (e.g. <c>ServiceUnavailable</c>) for infrastructure errors.
    /// </returns>
    Task<D2Result<AnonymizationOutcome>> AnonymizeUserAsync(
        Guid userId,
        CancellationToken ct = default);

    /// <summary>
    /// Runs an anonymization sweep over all <see cref="IOrgOwned"/>-marked entity types
    /// for the given organization subject, overwriting PII fields according to their
    /// registered <see cref="AnonymizationRule"/>.
    /// </summary>
    /// <param name="orgId">
    /// The id of the organization whose data is to be erased. Passing
    /// <see cref="Guid.Empty"/> returns <c>D2Result.ValidationFailed</c> with no database
    /// writes.
    /// </param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <returns>
    /// <c>D2Result.Ok(<see cref="AnonymizationOutcome"/>)</c> on success (including the
    /// idempotent re-run case), or <c>D2Result.ValidationFailed</c> when
    /// <paramref name="orgId"/> is <see cref="Guid.Empty"/>. Implementations may
    /// additionally return other <c>D2Result</c> failures (e.g.
    /// <c>ServiceUnavailable</c>) for infrastructure errors.
    /// </returns>
    Task<D2Result<AnonymizationOutcome>> AnonymizeOrgAsync(
        Guid orgId,
        CancellationToken ct = default);
}
