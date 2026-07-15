// HAND-WRITTEN CHASSIS (1 of 3). NOT generated.
//
// The terminal-hop transport seam for the spike. The generated SSE emit binding
// (NotificationCreatedSseEmitter, in SseEmit.cs) depends on ISseEmitSink; in a
// real system this would push onto a user's text/event-stream. Here the stub
// just CAPTURES the payload so the test can assert the event traversed every
// hop with its data intact. No real SSE runtime — this spike proves codegen
// completeness of the pipeline WIRING, not the SSE transport.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using D2.Spike.Push.V1;

namespace D2.Spike.Sse.Generated;

/// <summary>Terminal-hop transport contract the generated SSE emit binding calls.</summary>
public interface ISseEmitSink
{
    /// <summary>Deliver <paramref name="payload"/> to <paramref name="pushTarget"/>'s stream.</summary>
    Task SendAsync(string pushTarget, NotificationCreated payload, CancellationToken ct = default);
}

/// <summary>
/// In-memory capturing stub. Records every (pushTarget, payload) the pipeline
/// delivers so a test can assert the full multi-hop traversal.
/// </summary>
public sealed class StubSseEmitSink : ISseEmitSink
{
    private readonly ConcurrentQueue<(string PushTarget, NotificationCreated Payload)> _captured = new();

    /// <summary>Everything the pipeline has delivered to this sink, in order.</summary>
    public IReadOnlyCollection<(string PushTarget, NotificationCreated Payload)> Captured => _captured.ToArray();

    public Task SendAsync(string pushTarget, NotificationCreated payload, CancellationToken ct = default)
    {
        _captured.Enqueue((pushTarget, payload));
        return Task.CompletedTask;
    }
}
