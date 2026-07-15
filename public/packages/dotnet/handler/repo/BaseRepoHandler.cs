// -----------------------------------------------------------------------
// <copyright file="BaseRepoHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo;

using System;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using DcsvIo.D2.Result;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// EF-flavored <see cref="BaseHandler{TSelf, TInput, TOutput}"/> subclass
/// that converts database failures into typed <see cref="D2Result"/>
/// outcomes. Catches the BCL-typed
/// <see cref="DbUpdateConcurrencyException"/> directly; everything else is
/// routed through an injected <see cref="IDbExceptionClassifier"/> so the
/// base class stays provider-agnostic.
/// </summary>
/// <typeparam name="TSelf">CRTP self-type.</typeparam>
/// <typeparam name="TInput">The handler input type.</typeparam>
/// <typeparam name="TOutput">The handler output type.</typeparam>
/// <remarks>
/// <para>
/// Mapping (when
/// <see cref="BaseHandler{TSelf, TInput, TOutput}.RunCorePipelineAsync"/>
/// returns a non-null <c>CapturedException</c>):
/// </para>
/// <list type="bullet">
///   <item>
///     <see cref="DbUpdateConcurrencyException"/> →
///     <c>D2Result.ConcurrencyConflict()</c>.
///   </item>
///   <item>
///     Anything else → <c>classifier.Classify(ex)</c>; if it returns a
///     <see cref="DbFailureKind"/>, the matching factory is dispatched
///     (e.g. <see cref="DbFailureKind.UniqueViolation"/> →
///     <c>D2Result.UniqueViolation()</c>).
///   </item>
///   <item>
///     Classifier returned <c>null</c> → fall through unchanged
///     (BaseHandler already produced a
///     <c>D2Result.UnhandledException</c>).
///   </item>
/// </list>
/// <para>
/// Per-handler refinement: override <see cref="MapDbException"/> to attach
/// a domain-specific <c>TKMessage</c> + <c>InputError</c> for known
/// constraint failures (e.g. unique-violation on the user-email index →
/// return <c>UniqueViolation</c> with the localized "email already in use"
/// message and an <c>InputError</c> on the email field). Returning
/// <c>null</c> from the override falls back to the generic factory.
/// </para>
/// </remarks>
public abstract class BaseRepoHandler<TSelf, TInput, TOutput>
    : BaseHandler<TSelf, TInput, TOutput>
    where TSelf : BaseRepoHandler<TSelf, TInput, TOutput>
{
    private readonly IDbExceptionClassifier r_classifier;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BaseRepoHandler{TSelf, TInput, TOutput}"/> class.
    /// </summary>
    /// <param name="context">
    /// The handler context (request + logger) — DI-resolved per-request.
    /// </param>
    /// <param name="classifier">
    /// The provider-specific DB exception classifier (e.g. PostgreSQL,
    /// SQL Server). Composition roots register an implementation via the
    /// provider package's DI extension (e.g. <c>AddD2Postgres()</c>).
    /// </param>
    protected BaseRepoHandler(
        HandlerContext<TSelf> context,
        IDbExceptionClassifier classifier)
        : base(context)
    {
        r_classifier = classifier;
    }

    /// <inheritdoc/>
    public override async ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        var (result, exception) = await RunCorePipelineAsync(input, ct, options)
            .ConfigureAwait(false);
        if (exception is null)
            return result;

        var traceId = Context.Request.TraceId;

        // BCL-typed concurrency conflict — provider-agnostic, classified directly.
        if (exception is DbUpdateConcurrencyException)
        {
            var concurrencyOverride = MapDbException(exception, DbFailureKind.ConcurrencyConflict);
            return concurrencyOverride
                ?? D2Result<TOutput?>.ConcurrencyConflict(traceId: traceId);
        }

        // Provider-specific classification.
        var kind = r_classifier.Classify(exception);
        if (kind is null)
            return result;

        var custom = MapDbException(exception, kind.Value);
        if (custom is not null)
            return custom;

        return DispatchDefault(kind.Value, traceId);
    }

    /// <summary>
    /// Override to attach domain-specific messages / input errors to a
    /// known DB failure (e.g. translate a unique-violation on a specific
    /// index into a user-facing "email already in use" message). Return
    /// <c>null</c> to fall through to the generic factory.
    /// </summary>
    /// <param name="exception">The original captured exception.</param>
    /// <param name="kind">The classified failure kind.</param>
    /// <returns>A custom result, or <c>null</c> to use the default.</returns>
    protected virtual D2Result<TOutput?>? MapDbException(
        Exception exception,
        DbFailureKind kind) => null;

    // Wildcard arm throws rather than returning a degraded result. A future
    // DbFailureKind enum value added without a corresponding case here will
    // crash loudly at runtime (ArgumentOutOfRangeException) the first time
    // the new kind is dispatched, surfacing the gap immediately instead of
    // silently degrading to a generic UnhandledException. Compile-time
    // exhaustiveness would be preferable but CS8509 is masked by any wildcard
    // arm; the loud throw is the next-best safety net.
    private static D2Result<TOutput?> DispatchDefault(DbFailureKind kind, string? traceId)
        => kind switch
        {
            DbFailureKind.ConcurrencyConflict =>
                D2Result<TOutput?>.ConcurrencyConflict(traceId: traceId),
            DbFailureKind.UniqueViolation =>
                D2Result<TOutput?>.UniqueViolation(traceId: traceId),
            DbFailureKind.ForeignKeyViolation =>
                D2Result<TOutput?>.ForeignKeyViolation(traceId: traceId),
            DbFailureKind.NotNullViolation =>
                D2Result<TOutput?>.NotNullViolation(traceId: traceId),
            DbFailureKind.CheckViolation =>
                D2Result<TOutput?>.CheckViolation(traceId: traceId),
            DbFailureKind.Timeout =>
                D2Result<TOutput?>.DbTimeout(traceId: traceId),
            DbFailureKind.Deadlock =>
                D2Result<TOutput?>.DbDeadlock(traceId: traceId),
            DbFailureKind.ConnectionFailure =>
                D2Result<TOutput?>.DbConnectionFailure(traceId: traceId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind),
                kind,
                $"Unhandled {nameof(DbFailureKind)} value — add a dispatch arm in "
                + $"{nameof(BaseRepoHandler<TSelf, TInput, TOutput>)}.{nameof(DispatchDefault)}."),
        };
}
