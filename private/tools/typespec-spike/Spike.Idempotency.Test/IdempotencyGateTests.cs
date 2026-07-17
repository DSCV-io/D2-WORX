// HAND-WRITTEN CHASSIS (3 of 3). NOT generated.
//
// Drives the GENERATED SubmitOrderIdempotencyGate end to end and proves C1-C4:
//
//   C1 — same key twice  -> handler ran ONCE (second short-circuited), same result.
//   C2 — advance the fake clock past ttl -> a replay RE-INVOKES the handler.
//   C3 — missing/blank key -> a TYPED Outcome failure, NOT an exception.
//   C4 — the gate's store seam is INJECTED (the test supplies the fakes); the
//        generated gate has zero hardcoded store dependency.
//
// The gate, store seam, handler contract, and DTOs are all GENERATED from one
// @d2Idempotent decorator on the op. The ONLY hand-written idempotency code is
// the store fake + FakeClock + this counting handler + this test (the chassis).

using System;
using System.Threading.Tasks;
using D2.Spike.Idempotency.Generated;
using Xunit;

namespace Spike.Idempotency.Test;

public sealed class IdempotencyGateTests
{
    // ttlSeconds on the op is 300 -> the gate's internal TimeSpan is 5 minutes.
    private static readonly TimeSpan s_ttl = TimeSpan.FromSeconds(300);

    private static (SubmitOrderIdempotencyGate Gate, CountingSubmitOrderHandler Handler, FakeClock Clock, InMemoryIdempotencyStore Store) NewSut()
    {
        var clock = new FakeClock();
        var store = new InMemoryIdempotencyStore(clock);
        var handler = new CountingSubmitOrderHandler();
        // C4: the gate is constructed with INJECTED fakes — no hardcoded store.
        var gate = new SubmitOrderIdempotencyGate(handler, store);
        return (gate, handler, clock, store);
    }

    private static SubmitOrderInput Order(string key, string customer = "cust-1", int amount = 4200) =>
        new() { IdempotencyKey = key, CustomerId = customer, AmountCents = amount };

    // ---- C1 -----------------------------------------------------------------
    [Fact]
    public async Task C1_SameKeyTwice_HandlerRunsOnce_SameResultReturned()
    {
        var (gate, handler, _, _) = NewSut();
        var input = Order("idem-key-1");

        var first = await gate.ExecuteAsync(input);
        var second = await gate.ExecuteAsync(input); // SAME key

        // The handler body ran exactly ONCE — the second call short-circuited.
        Assert.Equal(1, handler.Invocations);

        // Both calls succeeded and returned the SAME result (replayed verbatim).
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal("order-1", first.Value!.OrderId);
        Assert.Equal("order-1", second.Value!.OrderId); // first run's id, replayed
        Assert.Equal(first.Value!.AmountCents, second.Value!.AmountCents);
    }

    [Fact]
    public async Task C1_DifferentKeys_HandlerRunsPerKey()
    {
        // Sanity / adversarial counterpart: distinct keys are NOT deduped.
        var (gate, handler, _, _) = NewSut();

        var a = await gate.ExecuteAsync(Order("key-A"));
        var b = await gate.ExecuteAsync(Order("key-B"));

        Assert.Equal(2, handler.Invocations);
        Assert.Equal("order-1", a.Value!.OrderId);
        Assert.Equal("order-2", b.Value!.OrderId);
    }

    // ---- C2 -----------------------------------------------------------------
    [Fact]
    public async Task C2_ReplayWithinTtl_ShortCircuits_AfterTtl_ReInvokes()
    {
        var (gate, handler, clock, _) = NewSut();
        var input = Order("idem-key-2");

        // First execution stores the result with the 300s ttl.
        var first = await gate.ExecuteAsync(input);
        Assert.Equal(1, handler.Invocations);
        Assert.Equal("order-1", first.Value!.OrderId);

        // Replay JUST shy of the ttl boundary -> still a HIT, handler NOT re-run.
        clock.Advance(s_ttl - TimeSpan.FromSeconds(1));
        var withinTtl = await gate.ExecuteAsync(input);
        Assert.Equal(1, handler.Invocations);
        Assert.Equal("order-1", withinTtl.Value!.OrderId);

        // Advance PAST the ttl -> the entry expires -> the replay RE-INVOKES.
        clock.Advance(TimeSpan.FromSeconds(2)); // now ttl+1 past the original
        var afterTtl = await gate.ExecuteAsync(input);
        Assert.Equal(2, handler.Invocations);          // re-invoked
        Assert.Equal("order-2", afterTtl.Value!.OrderId); // fresh execution, new id
    }

    [Fact]
    public async Task C2_ExactlyAtTtlBoundary_IsExpired()
    {
        // Adversarial boundary: at EXACTLY now+ttl the entry is expired (the
        // store uses a strict `clock < expiresAt`), so it re-invokes.
        var (gate, handler, clock, _) = NewSut();
        var input = Order("idem-key-boundary");

        await gate.ExecuteAsync(input);
        Assert.Equal(1, handler.Invocations);

        clock.Advance(s_ttl); // exactly at expiry
        await gate.ExecuteAsync(input);
        Assert.Equal(2, handler.Invocations);
    }

    // ---- C3 -----------------------------------------------------------------
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task C3_MissingOrBlankKey_TypedFailure_NotException(string badKey)
    {
        var (gate, handler, _, _) = NewSut();

        // A blank/whitespace key returns a TYPED failure outcome — no throw.
        var outcome = await gate.ExecuteAsync(Order(badKey));

        Assert.False(outcome.IsSuccess);
        Assert.Equal("idempotency_key_missing", outcome.FailureReason);
        Assert.Null(outcome.Value);
        // The handler never ran for a malformed key.
        Assert.Equal(0, handler.Invocations);
    }

    [Fact]
    public async Task C3_NullInput_TypedFailure_NotException()
    {
        var (gate, handler, _, _) = NewSut();

        var outcome = await gate.ExecuteAsync(null!);

        Assert.False(outcome.IsSuccess);
        Assert.Equal("input_null", outcome.FailureReason);
        Assert.Equal(0, handler.Invocations);
    }

    // ---- C4 -----------------------------------------------------------------
    [Fact]
    public async Task C4_StoreSeamIsInjected_GateHasNoHardcodedStore()
    {
        // The gate's ONLY ctor takes (handler, store) — both injected. There is
        // no parameterless ctor and no `new`-ed store inside the gate, so the
        // backing store is entirely caller-supplied (in-memory here; a
        // distributed cache in prod). We prove it by driving the gate purely
        // through the injected fake and observing the fake captured the write.
        var (gate, _, _, store) = NewSut();

        Assert.Equal(0, store.Count);
        await gate.ExecuteAsync(Order("idem-key-4"));

        // The generated gate wrote through the INJECTED store — proof it has no
        // hidden internal store of its own.
        Assert.Equal(1, store.Count);

        // Structural: the gate exposes exactly one ctor, arity 2 (handler+store).
        var ctors = typeof(SubmitOrderIdempotencyGate).GetConstructors();
        var only = Assert.Single(ctors);
        Assert.Equal(2, only.GetParameters().Length);
    }
}
