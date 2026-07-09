// -----------------------------------------------------------------------
// <copyright file="DrivableFakeTimeProvider.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Tests.Unit.AuthOutbound.WorkloadCertificate;

using System.Threading;
using Microsoft.Extensions.Time.Testing;

/// <summary>
/// A <see cref="FakeTimeProvider"/> that additionally raises a deterministic signal
/// every time code under test registers a timer — i.e. parks on a
/// <see cref="System.Threading.Tasks.Task.Delay(System.TimeSpan, System.TimeProvider, System.Threading.CancellationToken)"/>.
/// <para>
/// A background poll loop registers exactly one timer per iteration, so this signal
/// lets a driver advance the clock EXACTLY once per completed tick — pacing the drive
/// to the loop's REAL progress instead of free-running a concurrent nudger that races
/// the thread-pool scheduler. Starvation-proof: a saturated pool merely delays the
/// park signal; it can never cause a spurious advance, a lost tick, or a false
/// timeout. There is no wall-clock deadline anywhere on the drive path.
/// </para>
/// </summary>
internal sealed class DrivableFakeTimeProvider : FakeTimeProvider, IDisposable
{
    // One permit released per timer registration; a driver consumes one before each
    // advance, so it can never advance ahead of the loop it is driving. Counting
    // semantics buffer a registration that happens before the driver parks, so no
    // signal is ever lost.
    private readonly SemaphoreSlim _timerRegistered = new(0);

    /// <summary>
    /// Initializes a new instance of the <see cref="DrivableFakeTimeProvider"/> class.
    /// </summary>
    /// <param name="startDateTime">The initial UTC value of the fake clock.</param>
    public DrivableFakeTimeProvider(DateTimeOffset startDateTime)
        : base(startDateTime)
    {
    }

    /// <inheritdoc/>
    public override ITimer CreateTimer(
        TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = base.CreateTimer(callback, state, dueTime, period);

        // Signal AFTER the timer is registered with the base provider, so a driver
        // that wakes on this permit and immediately advances is guaranteed to fire
        // the just-registered timer.
        _timerRegistered.Release();

        return timer;
    }

    /// <summary>
    /// Awaits the next timer registration by code under test (a park on
    /// <c>Task.Delay(this, …)</c>). Buffered — a registration that happened before the
    /// caller began waiting is not lost.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when a timer has been registered.</returns>
    public Task WaitForTimerRegisteredAsync(CancellationToken ct = default)
        => _timerRegistered.WaitAsync(ct);

    /// <inheritdoc/>
    public void Dispose() => _timerRegistered.Dispose();
}
