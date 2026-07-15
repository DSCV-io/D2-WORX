// -----------------------------------------------------------------------
// <copyright file="SealedDeliveryFixtureRecorder.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian;

/// <summary>
/// Records the first opened sealed delivery for the sealed-messaging headline
/// integration test to await.
/// </summary>
public sealed class SealedDeliveryFixtureRecorder
{
    private readonly TaskCompletionSource<string> r_received =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets the first received content.</summary>
    public Task<string> Received => r_received.Task;

    /// <summary>Records a received content value (first wins).</summary>
    /// <param name="content">The opened message content.</param>
    public void Record(string content) => r_received.TrySetResult(content);
}
