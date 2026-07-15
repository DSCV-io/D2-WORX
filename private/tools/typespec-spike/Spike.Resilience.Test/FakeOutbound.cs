// HAND-WRITTEN CHASSIS (2 of 3). NOT generated.
//
// The controllable fake the generated pipeline wraps (implements the GENERATED
// ISubmitPaymentOutbound). It can be told to:
//   * fail TRANSIENTLY: throw for the first N calls, then succeed (the B2
//     transient test — the pipeline should retry and ultimately succeed), or
//   * fail SUSTAINEDLY: throw on every call (the B2 sustained test — the
//     breaker should eventually open and fast-fail).
// It counts CallAsync invocations so the test can assert how many times the
// outbound was actually reached (e.g. capped by the breaker once open).
//
// In prod this is the real downstream client; the spike only needs it to observe
// "how did the pipeline drive the outbound under fault?" — the resilience claim.

using System;
using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Resilience.Generated;

namespace Spike.Resilience.Test;

/// <summary>The downstream fault this fake simulates.</summary>
public enum FaultMode
{
    /// <summary>Never fails — every call succeeds.</summary>
    None,

    /// <summary>Fails the first <c>FailFirstN</c> calls, then succeeds (transient).</summary>
    TransientThenSucceed,

    /// <summary>Fails on every call (sustained).</summary>
    Always,
}

/// <summary>
/// Controllable <see cref="ISubmitPaymentOutbound"/> fake. <see cref="Calls"/> is
/// the number of times the outbound was actually invoked (NOT short-circuited by
/// an open breaker upstream).
/// </summary>
public sealed class FakeOutbound : ISubmitPaymentOutbound
{
    private readonly FaultMode _mode;
    private readonly int _failFirstN;
    private int _calls;

    /// <param name="mode">Which fault behavior to simulate.</param>
    /// <param name="failFirstN">For <see cref="FaultMode.TransientThenSucceed"/>, how many leading calls throw.</param>
    public FakeOutbound(FaultMode mode, int failFirstN = 0)
    {
        _mode = mode;
        _failFirstN = failFirstN;
    }

    /// <summary>How many times the outbound dependency was actually reached.</summary>
    public int Calls => _calls;

    public Task<SubmitPaymentOutput> CallAsync(SubmitPaymentInput input, CancellationToken ct = default)
    {
        var n = Interlocked.Increment(ref _calls);

        var shouldFail = _mode switch
        {
            FaultMode.Always => true,
            FaultMode.TransientThenSucceed => n <= _failFirstN,
            _ => false,
        };

        if (shouldFail)
            throw new InvalidOperationException($"simulated downstream fault (call #{n})");

        return Task.FromResult(new SubmitPaymentOutput
        {
            ConfirmationId = $"conf-{n}",
            AmountCents = input.AmountCents,
        });
    }
}
