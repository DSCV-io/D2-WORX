// HAND-WRITTEN CHASSIS (1 of 3). NOT generated.
//
// The stand-in resilience PRIMITIVES + the policy FACTORY. These implement the
// GENERATED abstraction (IAsyncResiliencePolicy / IResiliencePolicyFactory) and
// throw/catch the GENERATED BrokenCircuitException. In a real system these would
// be DcsvIo.D2.Resilience's policy + builder types (Polly-backed or bespoke);
// here they're minimal stand-ins so the spike proves the CODEGEN of the ordered
// composition + registry resolution, NOT the exact DcsvIo.D2.Resilience impl.
//
// This is the ONLY resilience-primitive code in the spike, and it's supplied by
// the TEST — the generated pipeline has zero hardcoded primitives (B4): it only
// ever sees the injected IResiliencePolicyFactory + IAsyncResiliencePolicy.
//
// Both primitives are deterministically testable:
//   * RetryPolicy takes an injectable delay strategy so the transient test runs
//     with a no-op delay (no real backoff sleep).
//   * CircuitBreakerPolicy takes an injectable now-provider so the open-window /
//     half-open reset is testable without a wall-clock sleep.

using System;
using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Resilience.Generated;

namespace Spike.Resilience.Test;

/// <summary>
/// Stand-in retry policy: runs the operation up to <c>maxAttempts</c> times,
/// waiting (via the injected delay strategy) between attempts. The LAST failure
/// is rethrown so an outer policy (e.g. a breaker) sees the exhausted retry as a
/// single failure. Implements the GENERATED <see cref="IAsyncResiliencePolicy"/>.
/// </summary>
public sealed class RetryPolicy : IAsyncResiliencePolicy
{
    private readonly int _maxAttempts;
    private readonly int _backoffMs;
    private readonly Func<int, CancellationToken, Task> _delay;

    /// <summary>Total operation attempts this policy has made (across all ExecuteAsync calls) — test introspection.</summary>
    public int TotalAttempts { get; private set; }

    /// <param name="maxAttempts">Max operation attempts (1 = no retry).</param>
    /// <param name="backoffMs">Delay between attempts, handed to the delay strategy.</param>
    /// <param name="delay">Injectable delay strategy (tests pass a no-op; prod waits).</param>
    public RetryPolicy(int maxAttempts, int backoffMs, Func<int, CancellationToken, Task>? delay = null)
    {
        _maxAttempts = maxAttempts < 1 ? 1 : maxAttempts;
        _backoffMs = backoffMs;
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            TotalAttempts++;
            try
            {
                return await operation(ct);
            }
            catch when (attempt < _maxAttempts)
            {
                // Budget remains -> back off and retry. A breaker-open signal from
                // an INNER policy would also be retried here (that is exactly the
                // breaker-inside-retry composition the flipped variant produces).
                await _delay(_backoffMs, ct);
            }
            // attempt == _maxAttempts -> the catch filter is false -> the last
            // exception propagates (retry budget exhausted).
        }
    }
}

/// <summary>
/// Stand-in circuit breaker: counts CONSECUTIVE failures; opens after
/// <c>failureThreshold</c> of them and fast-fails (throws the GENERATED
/// <see cref="BrokenCircuitException"/>) until <c>breakMs</c> has elapsed, after
/// which it allows ONE trial call (half-open). A success resets/closes it.
/// Time is read via an injected now-provider so the open window is testable
/// without sleeping. Implements the GENERATED <see cref="IAsyncResiliencePolicy"/>.
/// </summary>
public sealed class CircuitBreakerPolicy : IAsyncResiliencePolicy
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _break;
    private readonly Func<DateTimeOffset> _now;

    private int _consecutiveFailures;
    private DateTimeOffset? _openedAt;

    /// <summary>True while the breaker is OPEN and fast-failing — test introspection.</summary>
    public bool IsOpen => _openedAt is { } opened && _now() - opened < _break;

    /// <param name="failureThreshold">Consecutive failures that trip the breaker open.</param>
    /// <param name="breakMs">How long the breaker stays open before a half-open trial.</param>
    /// <param name="now">Injectable now-provider (tests advance a fake; prod uses UtcNow).</param>
    public CircuitBreakerPolicy(int failureThreshold, int breakMs, Func<DateTimeOffset>? now = null)
    {
        _failureThreshold = failureThreshold < 1 ? 1 : failureThreshold;
        _break = TimeSpan.FromMilliseconds(breakMs);
        _now = now ?? (() => DateTimeOffset.UtcNow);
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        // ---- fast-fail while OPEN (and still within the break window) ----------
        if (_openedAt is { } opened)
        {
            if (_now() - opened < _break)
                throw new BrokenCircuitException();

            // Break window elapsed -> half-open: allow ONE trial call below.
            _openedAt = null;
        }

        try
        {
            var result = await operation(ct);
            // Success -> close + reset the failure run.
            _consecutiveFailures = 0;
            return result;
        }
        catch
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= _failureThreshold)
                _openedAt = _now(); // trip OPEN
            throw;
        }
    }
}

/// <summary>
/// Stand-in <see cref="IResiliencePolicyFactory"/> the generated pipeline depends
/// on. The generated pipeline calls <see cref="CreateRetry"/> / <see cref="CreateBreaker"/>
/// with the registry-resolved tunables; this chassis factory mints the stand-in
/// primitives (with injected delay + now-provider so the tests are deterministic).
/// Prod swaps this for a DcsvIo.D2.Resilience-backed factory with no pipeline change.
/// </summary>
public sealed class StandInPolicyFactory : IResiliencePolicyFactory
{
    private readonly Func<DateTimeOffset> _now;
    private readonly Func<int, CancellationToken, Task> _delay;

    /// <summary>Last retry policy this factory created — test introspection (TotalAttempts).</summary>
    public RetryPolicy? LastRetry { get; private set; }

    /// <summary>Last breaker policy this factory created — test introspection (IsOpen).</summary>
    public CircuitBreakerPolicy? LastBreaker { get; private set; }

    /// <param name="now">Now-provider injected into every breaker (defaults to UtcNow).</param>
    /// <param name="delay">Delay strategy injected into every retry (defaults to Task.Delay).</param>
    public StandInPolicyFactory(Func<DateTimeOffset>? now = null, Func<int, CancellationToken, Task>? delay = null)
    {
        _now = now ?? (() => DateTimeOffset.UtcNow);
        _delay = delay ?? ((ms, ct) => Task.Delay(ms, ct));
    }

    public IAsyncResiliencePolicy CreateRetry(int maxAttempts, int backoffMs)
    {
        var policy = new RetryPolicy(maxAttempts, backoffMs, _delay);
        LastRetry = policy;
        return policy;
    }

    public IAsyncResiliencePolicy CreateBreaker(int failureThreshold, int breakMs)
    {
        var policy = new CircuitBreakerPolicy(failureThreshold, breakMs, _now);
        LastBreaker = policy;
        return policy;
    }
}
