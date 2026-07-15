// HAND-WRITTEN CHASSIS (2 of 3). NOT generated.
//
// The sample business-logic handler the generated gate wraps. It COUNTS its
// invocations so the test can assert exactly-once execution per idempotency key
// (C1) and re-execution after ttl expiry (C2). Each run mints a fresh order id
// so a replayed (cached) result is distinguishable from a re-executed one.
//
// In prod this is the real command handler; the spike only needs it to observe
// "did the handler body actually run?" — which is the whole idempotency claim.

using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Idempotency.Generated;

namespace Spike.Idempotency.Test;

/// <summary>
/// Counting <see cref="ISubmitOrderHandler"/>. <see cref="Invocations"/> is the
/// number of times the handler body actually ran — the gate's job is to keep
/// this at 1 for duplicate requests with the same key.
/// </summary>
public sealed class CountingSubmitOrderHandler : ISubmitOrderHandler
{
    private int _invocations;

    /// <summary>How many times the handler body has executed (NOT short-circuited).</summary>
    public int Invocations => _invocations;

    public Task<SubmitOrderOutput> HandleAsync(SubmitOrderInput input, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _invocations);
        // Fresh order id per real execution: a replayed result keeps the FIRST
        // run's id, so the test can tell a cache hit from a re-execution.
        return Task.FromResult(new SubmitOrderOutput
        {
            OrderId = $"order-{n}",
            AmountCents = input.AmountCents,
        });
    }
}
