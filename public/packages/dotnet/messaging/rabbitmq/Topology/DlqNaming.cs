// -----------------------------------------------------------------------
// <copyright file="DlqNaming.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Topology;

/// <summary>
/// Conventional names for the dead-letter exchange / queue that pair with
/// a primary queue, plus retry-tier naming used by the optional broker-level
/// retry topology.
/// </summary>
/// <remarks>
/// The convention is uniform across all queues so ops tooling
/// (<c>d2 msg decrypt --queue {q}.dlq</c>) and dashboards can rely on a
/// fixed shape. The lib does not allow overriding these names — they're
/// derived from the queue name, full stop.
/// </remarks>
internal static class DlqNaming
{
    /// <summary>Returns the DLX name for a primary queue.</summary>
    /// <param name="queueName">Primary queue name.</param>
    public static string DlxFor(string queueName) => $"{queueName}.dlx";

    /// <summary>Returns the DLQ name for a primary queue.</summary>
    /// <param name="queueName">Primary queue name.</param>
    public static string DlqFor(string queueName) => $"{queueName}.dlq";

    /// <summary>Returns the retry-tier exchange name (one per tier).</summary>
    /// <param name="queueName">Primary queue name.</param>
    /// <param name="tierIndex">Zero-based tier index.</param>
    public static string RetryTierExchangeFor(string queueName, int tierIndex)
        => $"{queueName}.retry.{tierIndex}";

    /// <summary>Returns the retry-tier queue name (one per tier).</summary>
    /// <param name="queueName">Primary queue name.</param>
    /// <param name="tierIndex">Zero-based tier index.</param>
    public static string RetryTierQueueFor(string queueName, int tierIndex)
        => $"{queueName}.retry.{tierIndex}";

    /// <summary>Returns the return exchange name — retry queues dead-letter
    /// here on TTL expiry; binding routes back to the primary queue.</summary>
    /// <param name="queueName">Primary queue name.</param>
    public static string RetryReturnExchangeFor(string queueName)
        => $"{queueName}.retry.return";
}
