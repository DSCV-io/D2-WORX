// -----------------------------------------------------------------------
// <copyright file="ExchangeRoute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Publishing;

/// <summary>Resolved exchange + routing key pair for a publish call.</summary>
/// <param name="Exchange">Canonical exchange name (descriptor or per-call
/// override).</param>
/// <param name="RoutingKey">Routing key (descriptor's default, or per-call
/// override). Empty string when fanout — bound queues don't filter on it.</param>
internal readonly record struct ExchangeRoute(string Exchange, string RoutingKey);
