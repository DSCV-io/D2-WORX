// -----------------------------------------------------------------------
// <copyright file="BaseHandlerTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Handler;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Handler;
using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Result;
using Xunit;

// Telemetry tests subscribe to the STATIC HandlerTelemetry.SR_Meter +
// SR_ActivitySource — concurrent test execution would race measurements
// across tests. Serialize within this collection so every metric / span
// captured by a TestMetricCollector / TestActivityCollector is unambiguously
// from THIS test, not bleed-over from a parallel one.
[Collection("HandlerTelemetrySerial")]
public sealed class BaseHandlerTests
{
    // ----------------------------------------------------------------------
    // 1. Happy path — Ok flows through unchanged
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_HappyPath_AutoInjectsTraceId()
    {
        // BaseHandler auto-injects TraceId on every result that crosses the
        // handler boundary so handlers don't need to thread
        // `traceId: this.Context.Request.TraceId` through every Ok/Created
        // call site. Failure paths inject via the typed factory call;
        // success paths inject via WithTraceId after ExecuteAsync returns.
        const string trace_id = "trace-happy";
        var request = new TestRequestContext { TraceId = trace_id };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(
                D2Result<TestOutput?>.Ok(new TestOutput { Tally = 7 })),
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeTrue();
        result.Data!.Tally.Should().Be(7);
        result.TraceId.Should().Be(trace_id);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_HandlerSetTraceId_NotOverwritten()
    {
        // Adversarial: if the handler already set a TraceId on its own
        // (e.g. it generated one for a synthetic request, or it's
        // forwarding a TraceId from an inner sub-handler), BaseHandler must
        // NOT overwrite it with Context.Request.TraceId.
        const string handler_trace = "trace-handler-explicit";
        var request = new TestRequestContext { TraceId = "trace-context-different" };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(
                D2Result<TestOutput?>.Ok(
                    new TestOutput { Tally = 7 },
                    traceId: handler_trace)),
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.TraceId.Should().Be(handler_trace);
    }

    [Fact]
    public async Task HandleAsync_HappyPath_RequestTraceIdIsNull_NoInjection()
    {
        // If the request itself has no TraceId (e.g. internal scheduled job
        // with no upstream trace), BaseHandler doesn't fabricate one —
        // result's TraceId stays null.
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(
                D2Result<TestOutput?>.Ok(new TestOutput { Tally = 7 })),
            request: new TestRequestContext { TraceId = null });

        var result = await handler.HandleAsync(new TestInput());

        result.TraceId.Should().BeNull();
    }

    // ----------------------------------------------------------------------
    // 2. Failure D2Result surfaces unchanged
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ExecuteReturnsFailure_FailureSurfacesUnchanged()
    {
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(
                D2Result<TestOutput?>.NotFound()));

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeFalse();
        result.IsNotFound.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // 3. Unrelated exception → UnhandledException + traceId
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ExecuteThrowsUnrelated_ReturnsUnhandledExceptionWithTraceId()
    {
        const string trace_id = "trace-throws";
        var (handler, logger) = BuildHandler(
            (_, _) => throw new InvalidOperationException("boom"),
            request: new TestRequestContext { TraceId = trace_id });

        var result = await handler.HandleAsync(new TestInput());

        result.IsUnhandledException.Should().BeTrue();
        result.TraceId.Should().Be(trace_id);

        // Adversarial: per §3.1, the log MUST NOT receive the raw Exception
        // object — `ex.Message` can carry connection strings / broker URIs /
        // OAuth tokens / raw user input. The HandlerThrew delegate now takes
        // `string exceptionType` only; the structured log entry's Exception
        // field stays null. On-call still gets the type name + duration; the
        // full stack/Message has to come from a separate, PII-scrubbed
        // diagnostic channel (or the consuming service's Serilog
        // destructuring policy applied to the activity error attribute).
        logger.Entries.Should().Contain(e => e.EventId.Id == 4 && e.Exception == null);
    }

    // ----------------------------------------------------------------------
    // 4. OCE — caller-canceled vs downstream-timeout split
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_OceWithCallerCanceled_ReturnsCanceled()
    {
        const string trace_id = "trace-cancel";
        var cts = new CancellationTokenSource();
        var (handler, logger) = BuildHandler(
            (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok());
            },
            request: new TestRequestContext { TraceId = trace_id });

        var result = await handler.HandleAsync(new TestInput(), cts.Token);

        result.IsCanceled.Should().BeTrue();
        result.TraceId.Should().Be(trace_id);
        logger.Entries.Should().Contain(e => e.EventId.Id == 3); // HandlerCanceled
    }

    [Fact]
    public async Task HandleAsync_OceWithoutTokenCancellation_ReturnsServiceUnavailable()
    {
        // Adversarial: an OperationCanceledException raised by a downstream
        // (HttpClient timeout, SQL command timeout, etc.) when our token
        // was NEVER canceled is a dependency-failure condition. Surfacing
        // it as UnhandledException would imply OUR bug; surfacing as
        // ServiceUnavailable correctly tells the caller "downstream is sick,
        // retry later." Critical correctness invariant.
        const string trace_id = "trace-downstream";
        var (handler, logger) = BuildHandler(
            (_, _) => throw new OperationCanceledException("downstream timed out"),
            request: new TestRequestContext { TraceId = trace_id });

        var result = await handler.HandleAsync(new TestInput()); // no cts

        result.IsServiceUnavailable.Should().BeTrue();
        result.TraceId.Should().Be(trace_id);
        logger.Entries.Should().Contain(e => e.EventId.Id == 7); // HandlerDownstreamTimeout
    }

    // ----------------------------------------------------------------------
    // 5. Scope pre-check — fires BEFORE ExecuteAsync runs
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_AllMatch_OneMissing_ReturnsForbiddenWithoutInvokingExecute()
    {
        // All-of check: caller holds read:foo but not write:foo — must be Forbidden.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["read:foo"]),
            TraceId = "trace-forbidden",
        };
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("must not be called"),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["write:foo"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.IsForbidden.Should().BeTrue();
        result.TraceId.Should().Be("trace-forbidden");
        handler.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_ScopeRequirementNull_NoCheck_ExecuteRuns()
    {
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions { ScopeRequirement = null });

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public void HandleAsync_ScopeRequirementEmptyScopes_ThrowsAtConstruction()
    {
        // Adversarial: empty Scopes set is now unconstructible (F5 guard).
        // The pipeline guard `is { Scopes.Count: > 0 }` remains as
        // defense-in-depth, but the constructor guard surfaces the
        // misconfiguration at compose time (before any handler call).
        // Regression-pins the F5 fix: fails without the constructor guard,
        // passes with it.
        var act = () => new ScopeRequirement(HandlerScopeMatch.All, new HashSet<string>());

        act.Should().Throw<ArgumentException>()
            .WithMessage("*ScopeRequirement.Scopes must contain at least one entry*");
    }

    [Fact]
    public async Task HandleAsync_AllMatch_AllScopesPresent_ExecuteRuns()
    {
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["a", "b", "c", "d"]),
        };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["a", "b", "c"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_AllMatch_SingleScopePresent_ExecuteRuns()
    {
        // All-of with exactly one required scope: caller has it — must pass.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["admin"]),
        };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["admin"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_AllMatch_PartialOverlap_ReturnsForbidden()
    {
        // Adversarial: holding 2 of 3 required scopes is NOT enough for All-of;
        // every declared scope must be present simultaneously.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["a", "b"]), // missing "c"
        };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["a", "b", "c"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_AllMatch_NonePresent_ReturnsForbidden()
    {
        // Adversarial: empty granted set with a non-empty All-of requirement
        // must return Forbidden immediately.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(),
        };
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("must not be called"),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["x", "y"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_AnyMatch_OneOfManyPresent_ExecuteRuns()
    {
        // Any-of: caller holds one of the declared scopes — must pass even
        // though the other declared scopes are absent.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["read"]),
        };
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(["read", "admin"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.Success.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(1);
    }

    [Fact]
    public async Task HandleAsync_AnyMatch_NonePresent_ReturnsForbidden()
    {
        // Adversarial: caller holds no overlap with the any-of set — must reject.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(["other"]),
        };
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("must not be called"),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(["read", "admin"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
    }

    [Fact]
    public async Task HandleAsync_AnyMatch_EmptyGrantedSet_ReturnsForbidden()
    {
        // Adversarial: Any-mode ScopeRequirement (e.g. ["read","admin"]) with a
        // caller whose Scopes is the EMPTY set must return Forbidden and must NOT
        // invoke ExecuteAsync — empty intersection of {} and {read,admin} is {}
        // which is zero overlap, so any-of fails.
        var request = new TestRequestContext
        {
            Scopes = new HashSet<string>(),
        };
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("must not be called"),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.Any,
                    new HashSet<string>(["read", "admin"])),
            },
            request: request);

        var result = await handler.HandleAsync(new TestInput());

        result.IsForbidden.Should().BeTrue();
        handler.ExecuteCallCount.Should().Be(0);
    }

    // ----------------------------------------------------------------------
    // 6. LogInput / LogOutput toggles
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_LogInputTrue_HandlerInvokedLogged()
    {
        var (handler, logger) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions { LogInput = true });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().Contain(e => e.EventId.Id == 1); // HandlerInvoked
    }

    [Fact]
    public async Task HandleAsync_LogInputFalse_HandlerInvokedNotLogged()
    {
        var (handler, logger) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions { LogInput = false });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().NotContain(e => e.EventId.Id == 1);
    }

    [Fact]
    public async Task HandleAsync_LogOutputTrue_HandlerReturnedLogged()
    {
        var (handler, logger) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions { LogOutput = true });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().Contain(e => e.EventId.Id == 2); // HandlerReturned
    }

    [Fact]
    public async Task HandleAsync_LogOutputFalse_HandlerReturnedNotLogged()
    {
        var (handler, logger) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions { LogOutput = false });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().NotContain(e => e.EventId.Id == 2);
    }

    // ----------------------------------------------------------------------
    // 7. Threshold breach logs — slow vs critical, with the if/else guard
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_SlowThresholdBreached_LogsSlowWarning()
    {
        var (handler, logger) = BuildHandler(
            async (_, _) =>
            {
                await Task.Delay(60, CancellationToken.None);
                return D2Result<TestOutput?>.Ok();
            },
            defaults: new HandlerOptions
            {
                SlowThreshold = TimeSpan.FromMilliseconds(10),
                CriticalThreshold = TimeSpan.FromSeconds(30),
            });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().Contain(e => e.EventId.Id == 6); // HandlerSlowThresholdExceeded
        logger.Entries.Should().NotContain(e => e.EventId.Id == 5); // not critical
    }

    [Fact]
    public async Task HandleAsync_CriticalThresholdBreached_LogsCriticalErrorOnly()
    {
        // Adversarial: the LogThresholdBreaches if/early-return structure
        // means when BOTH thresholds are breached, ONLY the critical log
        // fires. If the early return is ever removed, slow would also fire,
        // duplicating noise — this test pins the contract.
        var (handler, logger) = BuildHandler(
            async (_, _) =>
            {
                await Task.Delay(80, CancellationToken.None);
                return D2Result<TestOutput?>.Ok();
            },
            defaults: new HandlerOptions
            {
                SlowThreshold = TimeSpan.FromMilliseconds(5),
                CriticalThreshold = TimeSpan.FromMilliseconds(20),
            });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().Contain(e => e.EventId.Id == 5); // HandlerCriticalThresholdExceeded
        logger.Entries.Should().NotContain(e => e.EventId.Id == 6); // NOT slow
    }

    [Fact]
    public async Task HandleAsync_NeitherThresholdBreached_NoThresholdLogs()
    {
        var (handler, logger) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                SlowThreshold = TimeSpan.FromSeconds(10),
                CriticalThreshold = TimeSpan.FromSeconds(30),
            });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().NotContain(e => e.EventId.Id == 5);
        logger.Entries.Should().NotContain(e => e.EventId.Id == 6);
    }

    [Fact]
    public async Task HandleAsync_BothThresholdsNull_NoThresholdLogsRegardlessOfDuration()
    {
        // Adversarial: explicit opt-out (null thresholds) must skip the
        // checks even when the elapsed wall-clock would otherwise breach.
        var (handler, logger) = BuildHandler(
            async (_, _) =>
            {
                await Task.Delay(60, CancellationToken.None);
                return D2Result<TestOutput?>.Ok();
            },
            defaults: new HandlerOptions
            {
                SlowThreshold = null,
                CriticalThreshold = null,
            });

        await handler.HandleAsync(new TestInput());

        logger.Entries.Should().NotContain(e => e.EventId.Id == 5);
        logger.Entries.Should().NotContain(e => e.EventId.Id == 6);
    }

    // ----------------------------------------------------------------------
    // 8. Activity tags — tag emission for every IRequestContext field path
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_ActivityHasHandlerNameTag()
    {
        using var act = new TestActivityCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()));

        await handler.HandleAsync(new TestInput());

        act.Last.Should().NotBeNull();
        act.Last!.GetTagItem("d2.handler.name").Should().Be(typeof(TestHandler).Name);
    }

    [Fact]
    public async Task HandleAsync_UserIdPresent_EmitsUserIdTag()
    {
        using var act = new TestActivityCollector();
        var user_id = Guid.NewGuid();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            request: new TestRequestContext { UserId = user_id });

        await handler.HandleAsync(new TestInput());

        act.Last!.GetTagItem("d2.user_id").Should().Be(user_id.ToString());
    }

    [Fact]
    public async Task HandleAsync_OrgFieldsPresent_EmitsOrgTags()
    {
        using var act = new TestActivityCollector();
        var org_id = Guid.NewGuid();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            request: new TestRequestContext
            {
                OrgId = org_id,
                OrgType = OrgType.Customer,
                OrgRole = Role.Owner,
            });

        await handler.HandleAsync(new TestInput());

        act.Last!.GetTagItem("d2.org_id").Should().Be(org_id.ToString());
        act.Last.GetTagItem("d2.org_type").Should().Be("Customer");
        act.Last.GetTagItem("d2.org_role").Should().Be("Owner");
    }

    [Fact]
    public async Task HandleAsync_NoUserOrOrg_DoesNotEmitOptionalTags()
    {
        using var act = new TestActivityCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            request: new TestRequestContext()); // all nulls

        await handler.HandleAsync(new TestInput());

        act.Last!.GetTagItem("d2.user_id").Should().BeNull();
        act.Last.GetTagItem("d2.org_id").Should().BeNull();
        act.Last.GetTagItem("d2.org_type").Should().BeNull();
        act.Last.GetTagItem("d2.org_role").Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_Impersonating_EmitsFullImpersonatorChain()
    {
        using var act = new TestActivityCollector();
        var impersonator_id = Guid.NewGuid();
        var impersonator_org = Guid.NewGuid();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            request: new TestRequestContext
            {
                IsImpersonating = true,
                ImpersonationKind = ImpersonationKind.Consent,
                ImpersonatedBy = impersonator_id,
                ImpersonatorOrgId = impersonator_org,
                ImpersonatorOrgType = OrgType.Support,
                ImpersonatorOrgRole = Role.Agent,
            });

        await handler.HandleAsync(new TestInput());

        act.Last!.GetTagItem("d2.impersonating").Should().Be(true);
        act.Last.GetTagItem("d2.impersonation_kind").Should().Be("Consent");
        act.Last.GetTagItem("d2.impersonator_id").Should().Be(impersonator_id.ToString());
        act.Last.GetTagItem("d2.impersonator_org_id").Should().Be(impersonator_org.ToString());
        act.Last.GetTagItem("d2.impersonator_org_type").Should().Be("Support");
        act.Last.GetTagItem("d2.impersonator_org_role").Should().Be("Agent");
    }

    [Fact]
    public async Task HandleAsync_NotImpersonatingButImpersonatorOrgIdSet_DoesNotLeakTags()
    {
        // Adversarial: anomaly path — IsImpersonating is null/false but
        // some impersonator fields are populated (a misbehaving middleware,
        // or a stale envelope). The activity tags MUST be gated on the
        // IsImpersonating flag — leaking impersonator tags to non-impersonation
        // calls would corrupt audit traces.
        using var act = new TestActivityCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            request: new TestRequestContext
            {
                IsImpersonating = false,
                ImpersonatorOrgId = Guid.NewGuid(),
                ImpersonatorOrgType = OrgType.Admin,
            });

        await handler.HandleAsync(new TestInput());

        act.Last!.GetTagItem("d2.impersonating").Should().BeNull();
        act.Last.GetTagItem("d2.impersonator_org_id").Should().BeNull();
        act.Last.GetTagItem("d2.impersonator_org_type").Should().BeNull();
    }

    [Fact]
    public async Task HandleAsync_OnException_ActivityStatusIsError()
    {
        using var act = new TestActivityCollector();
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("kaboom"));

        await handler.HandleAsync(new TestInput());

        act.Last!.Status.Should().Be(ActivityStatusCode.Error);

        // Per §3.1: StatusDescription gets the exception type name only —
        // never `ex.Message` — because Message can carry connection strings /
        // broker URIs / OAuth tokens / raw user input. The OTel exporter
        // surfaces this as a string tag with the same leak surface as logs.
        act.Last.StatusDescription.Should().Be(nameof(InvalidOperationException));
    }

    [Fact]
    public async Task HandleAsync_OnCallerCancel_ActivityStatusIsErrorWithCanceledDescription()
    {
        using var act = new TestActivityCollector();
        var cts = new CancellationTokenSource();
        var (handler, _) = BuildHandler(
            (_, ct) =>
            {
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok());
            });

        await handler.HandleAsync(new TestInput(), cts.Token);

        act.Last!.Status.Should().Be(ActivityStatusCode.Error);
        act.Last.StatusDescription.Should().Be("canceled");
    }

    [Fact]
    public async Task HandleAsync_OnDownstreamTimeout_ActivityStatusIsErrorWithTimeoutDescription()
    {
        using var act = new TestActivityCollector();
        var (handler, _) = BuildHandler(
            (_, _) => throw new OperationCanceledException("timed out"));

        await handler.HandleAsync(new TestInput());

        act.Last!.Status.Should().Be(ActivityStatusCode.Error);
        act.Last.StatusDescription.Should().Be("downstream timeout");
    }

    // ----------------------------------------------------------------------
    // 9. Metrics — counter + histogram emission
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_OnSuccess_IncrementsInvokedAndSucceeded_NotFailed()
    {
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()));

        await handler.HandleAsync(new TestInput());

        metrics.CountFor("d2.handler.invoked").Should().Be(1);
        metrics.CountFor("d2.handler.succeeded").Should().Be(1);
        metrics.CountFor("d2.handler.failed").Should().Be(0);
        metrics.ValuesFor("d2.handler.duration").Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_OnFailureResult_IncrementsInvokedAndFailed_NotSucceeded()
    {
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.NotFound()));

        await handler.HandleAsync(new TestInput());

        metrics.CountFor("d2.handler.invoked").Should().Be(1);
        metrics.CountFor("d2.handler.succeeded").Should().Be(0);
        metrics.CountFor("d2.handler.failed").Should().Be(1);
        metrics.ValuesFor("d2.handler.duration").Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_OnException_IncrementsInvokedAndFailed_NotSucceeded()
    {
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            (_, _) => throw new InvalidOperationException("x"));

        await handler.HandleAsync(new TestInput());

        metrics.CountFor("d2.handler.invoked").Should().Be(1);
        metrics.CountFor("d2.handler.succeeded").Should().Be(0);
        metrics.CountFor("d2.handler.failed").Should().Be(1);
        metrics.ValuesFor("d2.handler.duration").Should().HaveCount(1);
    }

    [Fact]
    public async Task HandleAsync_OnScopePreCheckFail_IncrementsInvokedAndFailed_NoDuration()
    {
        // Adversarial: forbidden-at-entry path skips the stopwatch — verify
        // duration is NOT recorded for a request that never ran ExecuteAsync,
        // BUT invoked + failed are still counted (telemetry must show the
        // attempt happened).
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            (_, _) => new ValueTask<D2Result<TestOutput?>>(D2Result<TestOutput?>.Ok()),
            defaults: new HandlerOptions
            {
                ScopeRequirement = new ScopeRequirement(
                    HandlerScopeMatch.All,
                    new HashSet<string>(["nope"])),
            },
            request: new TestRequestContext { Scopes = new HashSet<string>() });

        await handler.HandleAsync(new TestInput());

        metrics.CountFor("d2.handler.invoked").Should().Be(1);
        metrics.CountFor("d2.handler.failed").Should().Be(1);
        metrics.CountFor("d2.handler.succeeded").Should().Be(0);
        metrics.ValuesFor("d2.handler.duration").Should().BeEmpty();
    }

    [Fact]
    public async Task HandleAsync_DurationHistogram_RecordsPositiveValue()
    {
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            async (_, _) =>
            {
                await Task.Delay(15, CancellationToken.None);
                return D2Result<TestOutput?>.Ok();
            });

        await handler.HandleAsync(new TestInput());

        var values = metrics.ValuesFor("d2.handler.duration");
        values.Should().ContainSingle();
        values[0].Should().BeGreaterThan(0);
    }

    // ----------------------------------------------------------------------
    // 10. Concurrency — 10 concurrent calls, no shared-state collisions
    // ----------------------------------------------------------------------

    [Fact]
    public async Task HandleAsync_TenConcurrentCalls_AllSucceedWithConsistentMetrics()
    {
        using var metrics = new TestMetricCollector();
        var (handler, _) = BuildHandler(
            async (_, _) =>
            {
                await Task.Yield();
                return D2Result<TestOutput?>.Ok(new TestOutput { Tally = 1 });
            });

        var tasks = Enumerable.Range(0, 10)
            .Select(_ => handler.HandleAsync(new TestInput()).AsTask())
            .ToArray();

        await Task.WhenAll(tasks);

        tasks.Should().AllSatisfy(t => t.Result.Success.Should().BeTrue());
        metrics.CountFor("d2.handler.invoked").Should().Be(10);
        metrics.CountFor("d2.handler.succeeded").Should().Be(10);
        metrics.CountFor("d2.handler.failed").Should().Be(0);
        metrics.ValuesFor("d2.handler.duration").Should().HaveCount(10);
        handler.ExecuteCallCount.Should().Be(10);
    }

    // ----------------------------------------------------------------------
    // 11. Architectural invariant — RunCorePipelineAsync is sealed (non-virtual)
    // ----------------------------------------------------------------------

    [Fact]
    public void RunCorePipelineAsync_IsNotVirtual_SubclassesCannotOverride()
    {
        // Adversarial: this is a sealed-by-design invariant — the entire
        // observability contract relies on subclasses being unable to skip
        // / replace the pipeline. Verify via reflection that the method
        // is NOT marked virtual / abstract.
        var method = typeof(BaseHandler<TestHandler, TestInput, TestOutput>)
            .GetMethod(
                "RunCorePipelineAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);

        method.Should().NotBeNull();
        method.IsVirtual.Should().BeFalse();
        method.IsAbstract.Should().BeFalse();
    }

    [Fact]
    public void HandleAsync_IsVirtual_SubclassesMayOverrideForRepoExceptionRemapping()
    {
        // Counterpart to the seal-check: HandleAsync IS virtual by design —
        // BaseRepoHandler overrides it to remap typed EF / PG exceptions.
        // Pin the virtual contract so a future accidental seal trips this.
        var method = typeof(BaseHandler<TestHandler, TestInput, TestOutput>)
            .GetMethod(nameof(IHandler<TestInput, TestOutput>.HandleAsync));

        method.Should().NotBeNull();
        method.IsVirtual.Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Test handler — a controllable BaseHandler subclass for testing the
    // sealed observability pipeline. The body delegate plus the optional
    // DefaultOptions override let each test sculpt the exact behavior it
    // needs without piling boilerplate into every test.
    // ----------------------------------------------------------------------

    private static (TestHandler Handler, TestLogger<TestHandler> Logger) BuildHandler(
        Func<TestInput, CancellationToken, ValueTask<D2Result<TestOutput?>>> body,
        HandlerOptions? defaults = null,
        TestRequestContext? request = null)
    {
        request ??= new TestRequestContext { TraceId = "trace-default" };
        var logger = new TestLogger<TestHandler>();
        var ctx = new HandlerContext<TestHandler>(request, logger);
        var handler = new TestHandler(ctx, body, defaults);
        return (handler, logger);
    }

    private sealed class TestInput
    {
    }

    private sealed class TestOutput
    {
        public int Tally { get; init; }
    }

    private sealed class TestHandler : BaseHandler<TestHandler, TestInput, TestOutput>
    {
        private readonly Func<TestInput, CancellationToken, ValueTask<D2Result<TestOutput?>>>
            r_body;

        private readonly HandlerOptions? r_defaultOptions;
        private int _executeCallCount;

        public TestHandler(
            HandlerContext<TestHandler> ctx,
            Func<TestInput, CancellationToken, ValueTask<D2Result<TestOutput?>>> body,
            HandlerOptions? defaults = null)
            : base(ctx)
        {
            r_body = body;
            r_defaultOptions = defaults;
        }

        public int ExecuteCallCount => _executeCallCount;

        protected override HandlerOptions DefaultOptions => r_defaultOptions ?? base.DefaultOptions;

        protected override ValueTask<D2Result<TestOutput?>> ExecuteAsync(
            TestInput input,
            CancellationToken ct)
        {
            Interlocked.Increment(ref _executeCallCount);
            return r_body(input, ct);
        }
    }
}
