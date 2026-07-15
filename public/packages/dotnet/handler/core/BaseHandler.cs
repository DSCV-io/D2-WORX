// -----------------------------------------------------------------------
// <copyright file="BaseHandler.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Result;

/// <summary>
/// Abstract base for every handler in the platform — CQRS handlers, repo
/// handlers, messaging consumers, scheduled jobs, anything handler-shaped.
/// Subclasses implement <see cref="ExecuteAsync"/> with the actual logic;
/// everything else (scope pre-check, activity span, log scope, stopwatch,
/// metrics, universal try/catch) is provided by the sealed observability
/// pipeline in <see cref="RunCorePipelineAsync"/>.
/// </summary>
/// <typeparam name="TSelf">
/// CRTP self-type — used for the <see cref="HandlerContext{T}"/> typed logger
/// + the <see cref="HandlerTelemetry"/> tag value.
/// </typeparam>
/// <typeparam name="TInput">The handler input type.</typeparam>
/// <typeparam name="TOutput">The handler output type.</typeparam>
public abstract class BaseHandler<TSelf, TInput, TOutput> : IHandler<TInput, TOutput>
    where TSelf : BaseHandler<TSelf, TInput, TOutput>
{
    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="BaseHandler{TSelf, TInput, TOutput}"/> class.
    /// </summary>
    /// <param name="context">
    /// The handler context (request + logger) — DI-resolved per-request.
    /// </param>
    protected BaseHandler(HandlerContext<TSelf> context)
    {
        Context = context;
    }

    /// <summary>Gets the per-request handler context (request + logger).</summary>
    protected IHandlerContext Context { get; }

    /// <summary>
    /// Gets the per-handler default options. Override to set handler-specific
    /// defaults (e.g. <c>LogInput = false</c> for handlers whose inputs carry
    /// PII unsupported by <c>[RedactData]</c>).
    /// </summary>
    protected virtual HandlerOptions DefaultOptions => new();

    /// <inheritdoc/>
    public virtual async ValueTask<D2Result<TOutput?>> HandleAsync(
        TInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        var (result, _) = await RunCorePipelineAsync(input, ct, options).ConfigureAwait(false);
        return result;
    }

    /// <summary>
    /// The observability + safety pipeline. Pre-checks required scopes,
    /// starts an <see cref="System.Diagnostics.Activity"/>, opens a log scope,
    /// records duration, increments counters, and runs <see cref="ExecuteAsync"/>
    /// inside a universal try/catch.
    /// </summary>
    /// <remarks>
    /// Non-virtual by design — subclasses cannot tamper with the pipeline.
    /// Returns the captured <see cref="Exception"/> alongside the result so
    /// EF-flavored subclasses (e.g. <c>BaseRepoHandler</c>) can remap typed
    /// EF / PG exceptions to <see cref="D2Result"/> failure codes from their
    /// own overridden <c>HandleAsync</c>.
    /// </remarks>
    /// <param name="input">The handler input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <param name="options">
    /// Per-call options; null falls through to <see cref="DefaultOptions"/>.
    /// </param>
    /// <returns>
    /// The result + the captured exception (null on success / handled-failure paths).
    /// </returns>
    protected async ValueTask<(D2Result<TOutput?> Result, Exception? CapturedException)>
        RunCorePipelineAsync(
            TInput input,
            CancellationToken ct,
            HandlerOptions? options)
    {
        var resolved = options ?? DefaultOptions;
        var traceId = Context.Request.TraceId;
        var handlerName = typeof(TSelf).Name;
        var handlerTag = new KeyValuePair<string, object?>("d2.handler.name", handlerName);

        if (resolved.ScopeRequirement is { Scopes.Count: > 0 } req)
        {
            var ok = req.Match == HandlerScopeMatch.All
                ? HandlerHasAllScopes(Context.Request.Scopes, req.Scopes)
                : HandlerHasAnyScope(Context.Request.Scopes, req.Scopes);
            if (!ok)
            {
                HandlerTelemetry.SR_Invoked.Add(1, handlerTag);
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);
                return (D2Result<TOutput?>.Forbidden(traceId: traceId), null);
            }
        }

        using var activity = HandlerTelemetry.SR_ActivitySource.StartActivity(handlerName);
        activity?.SetTag("d2.handler.name", handlerName);

        if (Context.Request.UserId is { } userId)
            activity?.SetTag("d2.user_id", userId.ToString());

        if (Context.Request.OrgId is { } orgId)
            activity?.SetTag("d2.org_id", orgId.ToString());

        if (Context.Request.OrgType is { } orgType)
            activity?.SetTag("d2.org_type", orgType.ToString());

        if (Context.Request.OrgRole is { } orgRole)
            activity?.SetTag("d2.org_role", orgRole.ToString());

        if (Context.Request.IsImpersonating == true)
        {
            activity?.SetTag("d2.impersonating", true);
            activity?.SetTag(
                "d2.impersonation_kind",
                Context.Request.ImpersonationKind?.ToString());

            if (Context.Request.ImpersonatedBy is { } impersonatorId)
                activity?.SetTag("d2.impersonator_id", impersonatorId.ToString());

            if (Context.Request.ImpersonatorOrgId is { } impersonatorOrgId)
                activity?.SetTag("d2.impersonator_org_id", impersonatorOrgId.ToString());

            if (Context.Request.ImpersonatorOrgType is { } impersonatorOrgType)
                activity?.SetTag("d2.impersonator_org_type", impersonatorOrgType.ToString());

            if (Context.Request.ImpersonatorOrgRole is { } impersonatorOrgRole)
                activity?.SetTag("d2.impersonator_org_role", impersonatorOrgRole.ToString());
        }

        // Open a logger scope so every log emitted inside ExecuteAsync (and
        // every BaseHandlerLog.* call below) carries the universal correlation
        // fields per §21.2 — traceId, userId, orgId, orgType, orgRole, plus
        // impersonation kind when relevant. IRequestContext doesn't carry a
        // separate correlationId field today (transport-middleware concern);
        // traceId IS the cross-service correlator.
        var scopeFields = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["d2.handler.name"] = handlerName,
        };

        if (traceId is not null)
            scopeFields["d2.trace_id"] = traceId;

        if (Context.Request.UserId is { } scopeUserId)
            scopeFields["d2.user_id"] = scopeUserId.ToString();

        if (Context.Request.OrgId is { } scopeOrgId)
            scopeFields["d2.org_id"] = scopeOrgId.ToString();

        if (Context.Request.OrgType is { } scopeOrgType)
            scopeFields["d2.org_type"] = scopeOrgType.ToString();

        if (Context.Request.OrgRole is { } scopeOrgRole)
            scopeFields["d2.org_role"] = scopeOrgRole.ToString();

        if (Context.Request.IsImpersonating == true)
        {
            scopeFields["d2.impersonation_kind"] =
                Context.Request.ImpersonationKind?.ToString();
        }

        using var logScope = Context.Logger.BeginScope(scopeFields);

        var stopwatch = Stopwatch.StartNew();
        HandlerTelemetry.SR_Invoked.Add(1, handlerTag);

        if (resolved.LogInput)
            BaseHandlerLog.HandlerInvoked(Context.Logger, handlerName, input);

        try
        {
            var result = await ExecuteAsync(input, ct).ConfigureAwait(false);
            stopwatch.Stop();

            HandlerTelemetry.SR_Duration.Record(stopwatch.Elapsed.TotalMilliseconds, handlerTag);

            if (result.Success)
                HandlerTelemetry.SR_Succeeded.Add(1, handlerTag);
            else
                HandlerTelemetry.SR_Failed.Add(1, handlerTag);

            LogThresholdBreaches(resolved, stopwatch.Elapsed, handlerName);

            if (resolved.LogOutput)
            {
                BaseHandlerLog.HandlerReturned(
                    Context.Logger,
                    handlerName,
                    result.Success ? "success" : "failure",
                    stopwatch.Elapsed.TotalMilliseconds);
            }

            // Auto-inject TraceId on every result that crosses the handler
            // boundary so downstream consumers (logs / responses / clients)
            // get correlation for free. Handlers that already set TraceId
            // explicitly keep their value (no override).
            if (result.TraceId is null && traceId is not null)
                result = result.WithTraceId(traceId);

            return (result, null);
        }
        catch (OperationCanceledException oce) when (ct.IsCancellationRequested)
        {
            // Caller canceled — semantically intentional cancellation.
            stopwatch.Stop();
            HandlerTelemetry.SR_Duration.Record(stopwatch.Elapsed.TotalMilliseconds, handlerTag);
            HandlerTelemetry.SR_Failed.Add(1, handlerTag);
            BaseHandlerLog.HandlerCanceled(
                Context.Logger,
                handlerName,
                stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, "canceled");
            return (D2Result<TOutput?>.Canceled(traceId: traceId), oce);
        }
        catch (OperationCanceledException oce)
        {
            // OCE without our token canceled = downstream timeout (HttpClient
            // timeout, SQL command timeout, internal handler watchdog, etc.).
            // That's a dependency-failure condition — surface as
            // ServiceUnavailable, not UnhandledException (which would imply a
            // bug in our own code).
            stopwatch.Stop();
            HandlerTelemetry.SR_Duration.Record(stopwatch.Elapsed.TotalMilliseconds, handlerTag);
            HandlerTelemetry.SR_Failed.Add(1, handlerTag);
            BaseHandlerLog.HandlerDownstreamTimeout(
                Context.Logger,
                handlerName,
                oce.GetType().Name,
                stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, "downstream timeout");
            return (D2Result<TOutput?>.ServiceUnavailable(traceId: traceId), oce);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            HandlerTelemetry.SR_Duration.Record(stopwatch.Elapsed.TotalMilliseconds, handlerTag);
            HandlerTelemetry.SR_Failed.Add(1, handlerTag);

            // Per §3.1: pass exception-type-name only — never `ex.Message` —
            // because Message can carry broker URIs / connection strings /
            // bearer tokens / raw user input. SetStatus description gets
            // the type name too (it ends up in OTel exporters as a string
            // tag, same leak surface).
            var exceptionType = ex.GetType().Name;
            BaseHandlerLog.HandlerThrew(
                Context.Logger,
                handlerName,
                exceptionType,
                stopwatch.Elapsed.TotalMilliseconds);
            activity?.SetStatus(ActivityStatusCode.Error, exceptionType);
            return (D2Result<TOutput?>.UnhandledException(traceId: traceId), ex);
        }
    }

    /// <summary>
    /// Implement the handler's actual logic. Called inside the observability
    /// pipeline (see <see cref="RunCorePipelineAsync"/>); exceptions surface as
    /// <see cref="D2Result.UnhandledException"/>.
    /// </summary>
    /// <param name="input">The handler input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The handler's result.</returns>
    protected abstract ValueTask<D2Result<TOutput?>> ExecuteAsync(
        TInput input,
        CancellationToken ct);

    private static bool HandlerHasAnyScope(
        IReadOnlySet<string> granted,
        IReadOnlySet<string> required)
    {
        foreach (var scope in required)
        {
            if (granted.Contains(scope))
                return true;
        }

        return false;
    }

    private static bool HandlerHasAllScopes(
        IReadOnlySet<string> granted,
        IReadOnlySet<string> required)
    {
        foreach (var scope in required)
        {
            if (!granted.Contains(scope))
                return false;
        }

        return true;
    }

    private void LogThresholdBreaches(HandlerOptions options, TimeSpan elapsed, string handlerName)
    {
        if (options.CriticalThreshold is { } critical && elapsed > critical)
        {
            BaseHandlerLog.HandlerCriticalThresholdExceeded(
                Context.Logger,
                handlerName,
                critical.TotalMilliseconds,
                elapsed.TotalMilliseconds);
            return;
        }

        if (options.SlowThreshold is { } slow && elapsed > slow)
        {
            BaseHandlerLog.HandlerSlowThresholdExceeded(
                Context.Logger,
                handlerName,
                slow.TotalMilliseconds,
                elapsed.TotalMilliseconds);
        }
    }
}
