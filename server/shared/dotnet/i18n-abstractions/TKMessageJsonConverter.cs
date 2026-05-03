// -----------------------------------------------------------------------
// <copyright file="TKMessageJsonConverter.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n;

using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads and writes <see cref="TKMessage"/> as a JSON object with shape
/// <c>{ "key": "..." }</c> (no parameters) or
/// <c>{ "key": "...", "params": { "name": "value", ... } }</c> (with parameters).
/// </summary>
/// <remarks>
/// Property names are camelCase to match the project's wire convention.
/// Deserialization is tolerant of property order and ignores unknown
/// properties. A missing <c>key</c> property is rejected — every message
/// MUST carry a key.
/// </remarks>
public sealed class TKMessageJsonConverter : JsonConverter<TKMessage>
{
    private const string _KEY_PROPERTY = "key";
    private const string _PARAMS_PROPERTY = "params";

    /// <inheritdoc/>
    public override TKMessage? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return null;
        }

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                $"Expected StartObject for {nameof(TKMessage)}, got {reader.TokenType}.");
        }

        string? key = null;
        Dictionary<string, string>? parameters = null;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                if (key is null)
                {
                    throw new JsonException(
                        $"{nameof(TKMessage)} JSON is missing required '{_KEY_PROPERTY}'.");
                }

                return new TKMessage(key, parameters);
            }

            if (reader.TokenType != JsonTokenType.PropertyName)
            {
                continue;
            }

            var propertyName = reader.GetString();
            reader.Read();

            if (string.Equals(propertyName, _KEY_PROPERTY, StringComparison.Ordinal))
            {
                key = reader.GetString();
            }
            else if (string.Equals(propertyName, _PARAMS_PROPERTY, StringComparison.Ordinal))
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    parameters = null;
                }
                else
                {
                    parameters = JsonSerializer.Deserialize<Dictionary<string, string>>(
                        ref reader, options);
                }
            }
            else
            {
                reader.Skip();
            }
        }

        throw new JsonException(
            $"Unexpected end of JSON while reading {nameof(TKMessage)}.");
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        TKMessage value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();
        writer.WriteString(_KEY_PROPERTY, value.Key);

        if (value.Parameters is { Count: > 0 })
        {
            writer.WritePropertyName(_PARAMS_PROPERTY);
            JsonSerializer.Serialize(writer, value.Parameters, options);
        }

        writer.WriteEndObject();
    }
}
