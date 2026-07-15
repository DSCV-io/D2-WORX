// HAND-WRITTEN CHASSIS (1 of 3). NOT generated.
//
// The store seam's in-memory fake + a controllable FakeClock. In a real system
// the generated gate's IIdempotencyStore would be a distributed/tiered cache
// (Redis); here the fake holds entries in a dictionary and evaluates ttl expiry
// against the injectable FakeClock so C2 (advance time past ttl -> miss) is a
// fast, deterministic test with no wall-clock sleep.
//
// This is the ONLY backing-store code in the spike, and it's supplied by the
// TEST — the generated gate has zero hardcoded store dependency (C4).

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Idempotency.Generated;

namespace Spike.Idempotency.Test;

/// <summary>Controllable clock: time only moves when the test advances it.</summary>
public sealed class FakeClock : IClock
{
    /// <summary>The current instant; the test mutates this to simulate ttl expiry.</summary>
    public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UnixEpoch;

    /// <summary>Move the clock forward by <paramref name="delta"/>.</summary>
    public void Advance(TimeSpan delta) => UtcNow += delta;
}

/// <summary>
/// In-memory <see cref="IIdempotencyStore"/> fake. Stores (payload, expiry) per
/// key; a lookup is a HIT only when the entry exists AND the FakeClock hasn't
/// passed its expiry. Evaluating expiry against the injected clock is what makes
/// the gate's ttl behavior testable without sleeping.
/// </summary>
public sealed class InMemoryIdempotencyStore : IIdempotencyStore
{
    private readonly Dictionary<string, (string Payload, DateTimeOffset ExpiresAt)> _entries = new();
    private readonly FakeClock _clock;

    public InMemoryIdempotencyStore(FakeClock clock) => _clock = clock;

    /// <summary>Total number of stored entries (live or expired) — test introspection.</summary>
    public int Count => _entries.Count;

    public ValueTask<(bool Found, string? Payload)> TryGetAsync(string key, CancellationToken ct = default)
    {
        if (_entries.TryGetValue(key, out var entry) && _clock.UtcNow < entry.ExpiresAt)
            return ValueTask.FromResult((true, (string?)entry.Payload));

        // Miss, or the entry has expired per the FakeClock (-> re-invoke handler).
        return ValueTask.FromResult((false, (string?)null));
    }

    public ValueTask SetAsync(string key, string payload, TimeSpan ttl, CancellationToken ct = default)
    {
        _entries[key] = (payload, _clock.UtcNow + ttl);
        return ValueTask.CompletedTask;
    }
}
