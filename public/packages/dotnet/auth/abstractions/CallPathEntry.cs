// -----------------------------------------------------------------------
// <copyright file="CallPathEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Abstractions;

/// <summary>
/// One hop in the propagated call-path: the identity of a workload/module that handled
/// the request, the kind of hop, and when it was reached. Operational telemetry only —
/// authority never reads the call-path. Serialized as a JSON object inside the
/// x-d2-context propagated subset (the first propagated list-of-records field).
/// </summary>
/// <param name="Id">The service / module id of the hop that appended this entry (its
/// OWN identity, e.g. "edge", "key-custodian"). Never request-controlled.</param>
/// <param name="Kind">Whether this hop was the edge, a cross-process workload, an
/// in-process module, or a system worker.</param>
/// <param name="Timestamp">When the hop was reached (UTC instant; wire form
/// DateTimeOffset for JSON interop). Consumers MUST convert to NodaTime.Instant at the
/// consumption boundary before any temporal arithmetic or comparison:
/// Timestamp.ToInstant().</param>
public sealed record CallPathEntry(
    string Id,
    CallPathKind Kind,
    DateTimeOffset Timestamp);
