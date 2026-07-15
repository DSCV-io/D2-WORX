// -----------------------------------------------------------------------
// <copyright file="MqSubscriptionsLoader.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Pure logic for parsing <c>mq-subscriptions.spec.json</c> into a
/// <see cref="MqSubscriptionsSpec"/>. JSON-shape validation only — semantic
/// validation lives in <see cref="MqEmitter"/>.
/// </summary>
internal static class MqSubscriptionsLoader
{
    private const string _SUBSCRIPTIONS_KEY = "subscriptions";
    private const string _CONSTANT = "constant";
    private const string _MESSAGE_TYPE = "messageType";
    private const string _QUEUE_NAME = "queueName";
    private const string _PATTERN = "pattern";
    private const string _ROUTING_KEY_BINDING = "routingKeyBinding";
    private const string _PREFETCH = "prefetch";
    private const string _IDEMPOTENCY = "idempotency";
    private const string _TIERED_RETRY = "tieredRetry";
    private const string _TIERS = "tiers";
    private const string _MAX_ATTEMPTS = "maxAttempts";

    public static LoadResult<MqSubscriptionsSpec> Load(string path, string json)
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

            if (!root.TryGetProperty(_SUBSCRIPTIONS_KEY, out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return new(null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    "missing required 'subscriptions' array property at root"));
            }

            var entries = ImmutableArray.CreateBuilder<MqSubscriptionEntry>();
            var index = 0;
            foreach (var entryEl in arr.EnumerateArray())
            {
                var (entry, diag) = ParseEntry(entryEl, fileName, index);
                if (diag is not null) return new(null, diag);
                entries.Add(entry!);
                index++;
            }

            return new(new MqSubscriptionsSpec(entries.ToImmutable()), null);
        }
        catch (JsonException ex)
        {
            return new(null, EmitDiagnostics.MalformedSpec(fileName, ex.Message));
        }
    }

    private static (MqSubscriptionEntry? Entry, EmitDiagnostic? Diagnostic) ParseEntry(
        JsonElement el, string fileName, int index)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"subscriptions[{index}] must be an object, got {el.ValueKind}"));
        }

        var indexLabel = $"[{index}]";

        if (!TryGetString(el, _CONSTANT, out var constant))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, indexLabel, _CONSTANT));
        if (!TryGetString(el, _MESSAGE_TYPE, out var messageType))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _MESSAGE_TYPE));
        if (!TryGetString(el, _QUEUE_NAME, out var queueName))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _QUEUE_NAME));
        if (!TryGetString(el, _PATTERN, out var pattern))
            return (null, EmitDiagnostics.MissingRequiredField(fileName, constant!, _PATTERN));

        TryGetString(el, _ROUTING_KEY_BINDING, out var routingKeyBinding);

        int? prefetch = null;
        if (el.TryGetProperty(_PREFETCH, out var prefetchEl) &&
            prefetchEl.ValueKind == JsonValueKind.Number &&
            prefetchEl.TryGetInt32(out var prefetchVal))
        {
            prefetch = prefetchVal;
        }

        bool? idempotency = null;
        if (el.TryGetProperty(_IDEMPOTENCY, out var idemEl))
        {
            if (idemEl.ValueKind == JsonValueKind.True) idempotency = true;
            else if (idemEl.ValueKind == JsonValueKind.False) idempotency = false;
        }

        TieredRetryConfig? tieredRetry = null;
        if (el.TryGetProperty(_TIERED_RETRY, out var retryEl))
        {
            var (cfg, retryDiag) = ParseTieredRetry(retryEl, fileName, constant!);
            if (retryDiag is not null) return (null, retryDiag);
            tieredRetry = cfg;
        }

        var entry = new MqSubscriptionEntry(
            Constant: constant!,
            MessageType: messageType!,
            QueueName: queueName!,
            Pattern: pattern!,
            RoutingKeyBinding: routingKeyBinding,
            Prefetch: prefetch,
            Idempotency: idempotency,
            TieredRetry: tieredRetry);
        return (entry, null);
    }

    private static (TieredRetryConfig? Config, EmitDiagnostic? Diagnostic) ParseTieredRetry(
        JsonElement el, string fileName, string subscriptionConstant)
    {
        if (el.ValueKind != JsonValueKind.Object)
        {
            return (null, EmitDiagnostics.MalformedSpec(
                fileName,
                $"subscription '{subscriptionConstant}' tieredRetry must be an object, "
                + $"got {el.ValueKind}"));
        }

        if (!el.TryGetProperty(_TIERS, out var tiersEl) ||
            tiersEl.ValueKind != JsonValueKind.Array)
        {
            return (null, EmitDiagnostics.MissingRequiredField(
                fileName, subscriptionConstant, $"{_TIERED_RETRY}.{_TIERS}"));
        }

        if (!el.TryGetProperty(_MAX_ATTEMPTS, out var maxEl) ||
            maxEl.ValueKind != JsonValueKind.Number ||
            !maxEl.TryGetInt32(out var maxAttempts))
        {
            return (null, EmitDiagnostics.MissingRequiredField(
                fileName, subscriptionConstant, $"{_TIERED_RETRY}.{_MAX_ATTEMPTS}"));
        }

        var tiers = ImmutableArray.CreateBuilder<string>();
        foreach (var tierEl in tiersEl.EnumerateArray())
        {
            if (tierEl.ValueKind != JsonValueKind.String)
            {
                return (null, EmitDiagnostics.MalformedSpec(
                    fileName,
                    $"subscription '{subscriptionConstant}' tieredRetry.tiers entry must be "
                    + $"a string, got {tierEl.ValueKind}"));
            }

            tiers.Add(tierEl.GetString()!);
        }

        return (new TieredRetryConfig(tiers.ToImmutable(), maxAttempts), null);
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
