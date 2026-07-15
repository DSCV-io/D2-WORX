// -----------------------------------------------------------------------
// <copyright file="MessagingJsonOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Shared <see cref="JsonSerializerOptions"/> for the messaging stack —
/// used to (de)serialize the wire body (the typed message itself) and the
/// <c>x-d2-failure-reason</c> DLQ header payload. Single instance so the
/// internal reflection cache is reused process-wide.
/// </summary>
public static class MessagingJsonOptions
{
    /// <summary>
    /// Canonical JSON conventions for messaging payloads: camelCase property
    /// names, omit null fields on write, ignore reference cycles, no
    /// pretty-printing.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
        WriteIndented = false,
    };
}
