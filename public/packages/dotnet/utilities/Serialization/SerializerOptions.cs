// -----------------------------------------------------------------------
// <copyright file="SerializerOptions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Predefined <see cref="JsonSerializerOptions"/> presets used across the
/// codebase. All instances are frozen at construction so they are safe to
/// share across threads and reuse without per-call allocation.
/// </summary>
public static class SerializerOptions
{
    /// <summary>
    /// Options that ignore reference cycles during serialization.
    /// </summary>
    public static readonly JsonSerializerOptions SR_IgnoreCycles = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    /// <summary>
    /// Options for HTTP/Web responses. camelCase property naming, enums as
    /// strings.
    /// </summary>
    public static readonly JsonSerializerOptions SR_Web = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Same as <see cref="SR_Web"/> but omits null properties from the output.
    /// </summary>
    public static readonly JsonSerializerOptions SR_WebIgnoreNull = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
