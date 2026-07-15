// -----------------------------------------------------------------------
// <copyright file="MqMessagesLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>mq-messages.spec.json</c> into a
/// <see cref="MqMessagesSpec"/>. JSON-shape validation only — semantic
/// validation (constant-name format, encryption-domain whitelist,
/// duplicate-constant detection, etc.) lives in <see cref="MqEmitter"/>.
/// </summary>
internal static class MqMessagesLoader
{
    private const string _MESSAGES_KEY = "messages";
    private const string _CONSTANT = "constant";
    private const string _MESSAGE_TYPE = "messageType";
    private const string _EXCHANGE = "exchange";
    private const string _EXCHANGE_TYPE = "exchangeType";
    private const string _ENCRYPTION = "encryption";
    private const string _ENCRYPTION_REASON = "encryptionReason";
    private const string _DEFAULT_ROUTING_KEY = "defaultRoutingKey";

    public static LoadResult<MqMessagesSpec> Load(string path, string json)
    {
        var fileName = Path.GetFileName(path);

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return new(null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"root must be a JSON object, got {root.ValueKind}"));
            }

            if (!root.TryGetProperty(_MESSAGES_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new(null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    "missing required 'messages' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<MqMessageEntry>();
            var index = 0;
            foreach (var entryEl in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(entryEl, fileName, index);
                if (diag is not null) return new(null, diag);
                entries.Add(entry!);
                index++;
            }

            return new(new MqMessagesSpec(entries.ToImmutable()), null);
        }
        catch (JsonException ex)
        {
            return new(null, EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (MqMessageEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement el, string fileName, int index)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName, $"messages[{index}] must be an object, got {el.ValueKind}"));
        }

        var indexLabel = $"[{index}]";

        if (!TryGetString(el, _CONSTANT, out var constant))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, indexLabel, _CONSTANT));
        if (!TryGetString(el, _MESSAGE_TYPE, out var messageType))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _MESSAGE_TYPE));
        if (!TryGetString(el, _EXCHANGE, out var exchange))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _EXCHANGE));
        if (!TryGetString(el, _EXCHANGE_TYPE, out var exchangeType))
        {
            return (
                null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _EXCHANGE_TYPE));
        }

        if (!TryGetString(el, _ENCRYPTION, out var encryption))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _ENCRYPTION));

        TryGetString(el, _ENCRYPTION_REASON, out var encryptionReason);
        TryGetString(el, _DEFAULT_ROUTING_KEY, out var defaultRoutingKey);

        var entry = new MqMessageEntry(
            Constant: constant!,
            MessageType: messageType!,
            Exchange: exchange!,
            ExchangeType: exchangeType!,
            Encryption: encryption!,
            EncryptionReason: encryptionReason,
            DefaultRoutingKey: defaultRoutingKey);
        return (entry, null);
    }

    private static bool TryGetString(JsonElement el, string name, out string? value)
    {
        if (el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            value = prop.GetString();
            return value is not null;
        }

        value = null;
        return false;
    }
}
