// -----------------------------------------------------------------------
// <copyright file="SerializerOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Serialization;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Provides predefined JSON serializer options.
/// </summary>
public static class SerializerOptions
{
    /// <summary>
    /// Gets JSON serializer options that ignore reference cycles during serialization.
    /// </summary>
    public static readonly JsonSerializerOptions SR_IgnoreCycles = new()
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles,
    };

    /// <summary>
    /// Gets JSON serializer options for HTTP/Web responses.
    /// Uses camelCase property naming and serializes enums as strings.
    /// </summary>
    public static readonly JsonSerializerOptions SR_Web = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Gets JSON serializer options for HTTP/Web responses that omit null properties.
    /// Uses camelCase property naming, serializes enums as strings, and ignores null values.
    /// </summary>
    public static readonly JsonSerializerOptions SR_WebIgnoreNull = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };
}
