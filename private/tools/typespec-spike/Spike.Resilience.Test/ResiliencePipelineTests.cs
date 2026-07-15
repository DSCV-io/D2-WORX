// HAND-WRITTEN CHASSIS (3 of 3). NOT generated.
//
// Drives the GENERATED SubmitPaymentResiliencePipeline end to end and proves
// B1-B4:
//
//   B1 — ordered composition: the GENERATED ComposedOrder == the spec's profile
//        order (retry-fast -> breaker-standard, innermost->outermost). A separate
//        chassis-only test proves order is BEHAVIORALLY load-bearing (flipping
//        retry<->breaker flips the observable outbound-call pattern under fault),
//        and the runner compiles the order-FLIPPED spec to show the generated
//        ComposedOrder flips to match.
//   B2 — behavioral: transient faults within the retry budget -> the pipeline
//        RETRIES and ultimately SUCCEEDS; sustained faults past the breaker
//        threshold -> the breaker OPENS and the pipeline FAST-FAILS (typed).
//   B3 — registry resolution: the pipeline's tunables come ONLY from the
//        GENERATED ResilienceProfiles constants (no magic numbers at the call
//        site). The UNKNOWN-profile build failure (the compile-time half of B3)
//        is proven by the runner compiling resilience-unknown.tsp.
//   B4 — the generated pipeline targets the resilience ABSTRACTION: its ctor
//        takes (ISubmitPaymentOutbound, IResiliencePolicyFactory) only — no
//        hardcoded primitive. We drive it purely through injected stand-ins.
//
// The pipeline, abstraction, resolved-registry constants, DTOs, outbound seam,
// and wiring are all GENERATED from one @d2Resilience decorator. The ONLY
// hand-written resilience code is the stand-in primitives + factory + fake
// outbound + this test (the chassis).

using System;
using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Resilience.Generated;
using Xunit;

namespace Spike.Resilience.Test;

public sealed class ResiliencePipelineTests
{
    // Registry-resolved tunables for the header variant (retry-fast: 3 attempts,
    // 50ms; breaker-standard: threshold 3, 30000ms). The tests read them from the
    // GENERATED constants so the asserts track the registry, not a local copy.
    private const string EXPECTED_ORDER = "retry-fast -> breaker-standard";

    // No-op delay so the retry backoff doesn't actually sleep in tests.
    private static readonly Func<int, CancellationToken, Task> s_noDelay = (_, _) => Task.CompletedTask;

    // A frozen clock so the breaker's open window never elapses mid-test (the
    // sustained test only needs trip-then-immediately-fast-fail).
    private static Func<DateTimeOffset> FrozenClock() => () => DateTimeOffset.UnixEpoch;

    private static SubmitPaymentInput Payment(string id = "pay-1", int amount = 4200) =>
        new() { PaymentId = id, AmountCents = amount };

    private static (SubmitPaymentResiliencePipeline Pipeline, FakeOutbound Outbound, StandInPolicyFactory Factory) NewSut(
        FakeOutbound outbound,
        Func<DateTimeOffset>? now = null)
    {
        var factory = new StandInPolicyFactory(now ?? FrozenClock(), s_noDelay);
        // B4: the pipeline is constructed with INJECTED stand-ins — no hardcoded primitive.
        var pipeline = new SubmitPaymentResiliencePipeline(outbound, factory);
        return (pipeline, outbound, factory);
    }

    // ---- B1: generated composition order == spec order ----------------------
    [Fact]
    public void B1_GeneratedComposedOrder_MatchesSpecProfileOrder()
    {
        // The GENERATED pipeline exposes the composed order it was emitted with.
        // It must equal the spec's @d2Resilience("retry-fast","breaker-standard")
        // order (innermost -> outermost). This is the codegen claim: the emitted
        // composition order IS the declared order, not a hard-coded default.
        Assert.Equal(EXPECTED_ORDER, SubmitPaymentResiliencePipeline.ComposedOrder);
    }

    // ---- B1: composition ORDER is behaviorally load-bearing -----------------
    // Chassis-only (no generated pipeline) so it's independent of which spec
    // variant was generated. Proves that flipping retry<->breaker flips the
    // observable behavior under sustained fault — which is exactly what the
    // generated composition order controls.
    [Fact]
    public async Task B1_CompositionOrder_IsBehaviorallyLoadBearing()
    {
        // breaker(retry(call)) — retry INNERMOST (the header variant's order).
        // Each pipeline run does a full retry burst (3 outbound calls) before the
        // breaker counts ONE failure, so the breaker takes 3 runs (9 calls) to open.
        {
            var calls = 0;
            Func<CancellationToken, Task<int>> raw = _ =>
            {
                calls++;
                throw new InvalidOperationException("fault");
            };
            var retry = new RetryPolicy(maxAttempts: 3, backoffMs: 0, s_noDelay);
            var breaker = new CircuitBreakerPolicy(failureThreshold: 3, breakMs: 30000, FrozenClock());

            // run the composed pipeline 3 times; each run exhausts retry (3 calls).
            for (var i = 0; i < 3; i++)
                await SwallowAsync(() => breaker.ExecuteAsync(ct => retry.ExecuteAsync(raw, ct)));

            Assert.True(breaker.IsOpen);   // opened after 3 retry-bursts
            Assert.Equal(9, calls);        // 3 runs * 3 attempts
        }

        // retry(breaker(call)) — breaker INNERMOST (the FLIPPED variant's order).
        // The breaker sees EACH attempt, so it opens DURING the first run's 3
        // attempts; subsequent attempts fast-fail without reaching the call.
        {
            var calls = 0;
            Func<CancellationToken, Task<int>> raw = _ =>
            {
                calls++;
                throw new InvalidOperationException("fault");
            };
            var retry = new RetryPolicy(maxAttempts: 3, backoffMs: 0, s_noDelay);
            var breaker = new CircuitBreakerPolicy(failureThreshold: 3, breakMs: 30000, FrozenClock());

            // a SINGLE composed run: retry drives the breaker.
            await SwallowAsync(() => retry.ExecuteAsync(ct => breaker.ExecuteAsync(raw, ct)));

            Assert.True(breaker.IsOpen);   // opened within the first run
            Assert.Equal(3, calls);        // breaker opened on the 3rd attempt; no fast-fail reached the call
        }

        // SAME primitives, SAME fault, DIFFERENT composition order -> DIFFERENT
        // observable call pattern (9 calls / 3 runs vs 3 calls / 1 run). Order matters.
    }

    // ---- B2: transient fault within budget -> retried -> SUCCESS ------------
    [Fact]
    public async Task B2_TransientFaultWithinRetryBudget_PipelineRetriesAndSucceeds()
    {
        // Fail the first 2 calls, succeed on the 3rd. retry-fast allows 3 attempts,
        // so breaker(retry(call)) retries through the transient faults and succeeds.
        var outbound = new FakeOutbound(FaultMode.TransientThenSucceed, failFirstN: 2);
        var (pipeline, _, factory) = NewSut(outbound);

        var outcome = await pipeline.ExecuteAsync(Payment());

        Assert.True(outcome.IsSuccess);                       // ultimately succeeded
        Assert.Equal("conf-3", outcome.Value!.ConfirmationId); // the 3rd (successful) call
        Assert.Equal(3, outbound.Calls);                       // 2 fails + 1 success
        Assert.Equal(3, factory.LastRetry!.TotalAttempts);     // retry made all 3 attempts
        Assert.False(factory.LastBreaker!.IsOpen);             // never tripped (eventual success)
    }

    [Fact]
    public async Task B2_TransientFaultExceedingRetryBudget_PipelineFailsTyped()
    {
        // Fail the first 3 calls — that EXHAUSTS the 3-attempt retry budget, so the
        // single pipeline run fails (typed) rather than succeeding.
        var outbound = new FakeOutbound(FaultMode.TransientThenSucceed, failFirstN: 3);
        var (pipeline, _, _) = NewSut(outbound);

        var outcome = await pipeline.ExecuteAsync(Payment());

        Assert.False(outcome.IsSuccess);
        Assert.Equal(3, outbound.Calls); // all 3 attempts used, all failed
        Assert.StartsWith("dependency_failed:", outcome.FailureReason);
    }

    // ---- B2: sustained fault past threshold -> breaker OPENS -> fast-fail ----
    [Fact]
    public async Task B2_SustainedFault_BreakerOpens_PipelineFastFailsTyped()
    {
        // Always fails. breaker(retry(call)) with threshold 3: each pipeline run
        // exhausts retry (3 calls) and counts as ONE breaker failure. After 3 runs
        // the breaker opens; the 4th run fast-fails WITHOUT reaching the outbound.
        var outbound = new FakeOutbound(FaultMode.Always);
        var (pipeline, _, factory) = NewSut(outbound);

        // 3 runs to trip the breaker (each: retry budget exhausted = 1 failure).
        for (var i = 0; i < 3; i++)
        {
            var o = await pipeline.ExecuteAsync(Payment());
            Assert.False(o.IsSuccess);
        }

        Assert.True(factory.LastBreaker!.IsOpen); // tripped after 3 sustained failures
        var callsBeforeOpen = outbound.Calls;     // 3 runs * 3 attempts = 9
        Assert.Equal(9, callsBeforeOpen);

        // 4th run: breaker is OPEN -> typed circuit_open fast-fail, outbound NOT reached.
        var fastFail = await pipeline.ExecuteAsync(Payment());
        Assert.False(fastFail.IsSuccess);
        Assert.Equal("circuit_open", fastFail.FailureReason);
        Assert.Equal(callsBeforeOpen, outbound.Calls); // unchanged — the call was short-circuited
    }

    [Fact]
    public async Task B2_BreakerHalfOpen_AfterWindow_AllowsRecovery()
    {
        // Adversarial / completeness: once the break window elapses the breaker
        // half-opens and a now-healthy dependency recovers. Uses a MUTABLE clock.
        var now = DateTimeOffset.UnixEpoch;
        var clock = new MutableClock(now);

        // Fail the first 9 calls (enough to trip the breaker across 3 runs), then
        // succeed — so the post-window trial call sees a healthy dependency.
        var outbound = new FakeOutbound(FaultMode.TransientThenSucceed, failFirstN: 9);
        var (pipeline, _, factory) = NewSut(outbound, clock.Now);

        for (var i = 0; i < 3; i++)
            await pipeline.ExecuteAsync(Payment());
        Assert.True(factory.LastBreaker!.IsOpen);

        // Advance PAST the 30s break window -> half-open -> the trial call (now the
        // 10th outbound call) succeeds -> pipeline recovers.
        clock.Advance(TimeSpan.FromSeconds(31));
        var recovered = await pipeline.ExecuteAsync(Payment());

        Assert.True(recovered.IsSuccess);
        Assert.Equal("conf-10", recovered.Value!.ConfirmationId);
        Assert.False(factory.LastBreaker!.IsOpen); // closed again after the successful trial
    }

    // ---- B3: tunables come from the generated registry constants ------------
    [Fact]
    public void B3_RegistryResolution_TunablesFromGeneratedConstants_NoMagicNumbers()
    {
        // The resolved registry is materialized as GENERATED constants; the
        // pipeline reads ONLY these. Asserting their values proves the spec's
        // profile names resolved to the registry tuning (not call-site literals).
        Assert.Equal(3, ResilienceProfiles.RetryFast.MaxAttempts);
        Assert.Equal(50, ResilienceProfiles.RetryFast.BackoffMs);
        Assert.Equal(3, ResilienceProfiles.BreakerStandard.FailureThreshold);
        Assert.Equal(30000, ResilienceProfiles.BreakerStandard.BreakMs);
        // (The UNKNOWN-profile compile failure — the other half of B3 — is proven
        //  by the runner compiling resilience-unknown.tsp, captured in the verdict.)
    }

    // ---- B4: the pipeline targets the abstraction, not a raw primitive -------
    [Fact]
    public async Task B4_PipelineTargetsAbstraction_CtorTakesFactoryAndOutboundOnly()
    {
        // Drive the generated pipeline purely through INJECTED stand-ins — proof
        // it has no hidden hardcoded primitive of its own.
        var outbound = new FakeOutbound(FaultMode.None);
        var (pipeline, _, factory) = NewSut(outbound);

        var ok = await pipeline.ExecuteAsync(Payment());
        Assert.True(ok.IsSuccess);
        // The pipeline minted its policies via the injected factory (proof it
        // depends on the abstraction, not a `new`-ed primitive).
        Assert.NotNull(factory.LastRetry);
        Assert.NotNull(factory.LastBreaker);

        // Structural: the generated pipeline exposes exactly one ctor, arity 2
        // (outbound + factory) — both abstractions, no primitive parameter.
        var ctors = typeof(SubmitPaymentResiliencePipeline).GetConstructors();
        var only = Assert.Single(ctors);
        Assert.Equal(2, only.GetParameters().Length);
        Assert.Equal(typeof(ISubmitPaymentOutbound), only.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(IResiliencePolicyFactory), only.GetParameters()[1].ParameterType);
    }

    // ---- adversarial: null input -> typed failure, no outbound call ----------
    [Fact]
    public async Task NullInput_TypedFailure_NotException()
    {
        var outbound = new FakeOutbound(FaultMode.None);
        var (pipeline, _, _) = NewSut(outbound);

        var outcome = await pipeline.ExecuteAsync(null!);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("input_null", outcome.FailureReason);
        Assert.Equal(0, outbound.Calls);
    }

    // ---- helpers ------------------------------------------------------------
    private static async Task SwallowAsync<T>(Func<Task<T>> action)
    {
        try
        {
            await action();
        }
        catch
        {
            // Intentionally swallowed — these chassis-only order tests assert on
            // the breaker/call-count side effects, not the thrown exception.
        }
    }
}

/// <summary>Mutable now-provider for the half-open recovery test.</summary>
internal sealed class MutableClock
{
    private DateTimeOffset _now;

    public MutableClock(DateTimeOffset start) => _now = start;

    public Func<DateTimeOffset> Now => () => _now;

    public void Advance(TimeSpan delta) => _now += delta;
}
