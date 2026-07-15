// -----------------------------------------------------------------------
// <copyright file="MqEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.SourceGen;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Pure logic for emitting the messaging spec output: <c>MqMessages</c> +
/// <c>MqSubscriptions</c> static classes (string constants) AND a runtime
/// registry pair (<c>MqMessagesRegistry</c> + <c>MqSubscriptionsRegistry</c>)
/// mapping each constant to its full descriptor record. Stateless, unit-
/// testable in isolation.
/// </summary>
internal static class MqEmitter
{
    private const string _TARGET_NAMESPACE = "DcsvIo.D2.Messaging";
    private const string _PATTERNS_FQN = "DcsvIo.D2.Messaging.QueuePattern";
    private const string _PLAINTEXT_LITERAL = "plaintext";

    // Closed vocabulary — kept inline so unit tests can pin behavior without
    // pulling enums from a downstream assembly.
    private static readonly HashSet<string> sr_exchangeTypes =
        new(StringComparer.Ordinal) { "fanout", "topic", "direct" };

    private static readonly HashSet<string> sr_patterns = new(StringComparer.Ordinal)
    {
        "CompetingConsumer",
        "FanoutExclusiveAutoDelete",
        "DurableShared",
    };

    /// <summary>
    /// Emits the full messaging spec output. Returns 1-2 generated files:
    /// <c>MqMessages.g.cs</c> always; <c>MqSubscriptions.g.cs</c> when there
    /// are any subscriptions (or always for symmetry).
    /// </summary>
    /// <param name="messages">Parsed messages spec.</param>
    /// <param name="subscriptions">Parsed subscriptions spec.</param>
    /// <param name="encryptionDomains">EncryptionDomains constant values
    /// (e.g. ["audit", "notifications", "courier"]) extracted from the
    /// compilation symbol. The emitter validates each message's encryption
    /// value against this set + the literal "plaintext".</param>
    /// <returns>Two emit results plus aggregated diagnostics.</returns>
    public static (EmitResult Messages, EmitResult Subscriptions) Emit(
        MqMessagesSpec messages,
        MqSubscriptionsSpec subscriptions,
        IReadOnlyList<string> encryptionDomains)
    {
        var msgDiags = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var subDiags = ImmutableArray.CreateBuilder<EmitDiagnostic>();

        var validMessages = ValidateMessages(messages, encryptionDomains, msgDiags);
        var validSubs = ValidateSubscriptions(subscriptions, validMessages, subDiags);

        var messagesSrc = EmitMessagesClass(validMessages);
        var subsSrc = EmitSubscriptionsClass(validSubs);

        return (
            new EmitResult("MqMessages.g.cs", messagesSrc, msgDiags.ToImmutable()),
            new EmitResult("MqSubscriptions.g.cs", subsSrc, subDiags.ToImmutable()));
    }

    private static List<MqMessageEntry> ValidateMessages(
        MqMessagesSpec spec,
        IReadOnlyList<string> encryptionDomains,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var validEncryption = new HashSet<string>(encryptionDomains, StringComparer.Ordinal)
        {
            _PLAINTEXT_LITERAL,
        };
        var valid = new List<MqMessageEntry>();

        foreach (var entry in spec.Messages)
        {
            if (!ValidateConstant(entry.Constant, out var constReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstantName(
                    "mq-messages.spec.json", entry.Constant, constReason));
                continue;
            }

            if (!seen.Add(entry.Constant))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstant(
                    "mq-messages.spec.json", entry.Constant));
                continue;
            }

            if (!sr_exchangeTypes.Contains(entry.ExchangeType))
            {
                diagnostics.Add(EmitDiagnostics.UnknownExchangeType(
                    entry.Constant, entry.ExchangeType));
                continue;
            }

            if (!validEncryption.Contains(entry.Encryption))
            {
                var validList = string.Join(", ",
                    encryptionDomains.OrderBy(s => s, StringComparer.Ordinal));
                diagnostics.Add(EmitDiagnostics.UnknownEncryption(
                    entry.Constant, entry.Encryption, validList));
                continue;
            }

            if (string.Equals(entry.Encryption, _PLAINTEXT_LITERAL, StringComparison.Ordinal))
            {
                if (entry.EncryptionReason.Falsey()
                    || (entry.EncryptionReason?.Trim().Length ?? 0) < 10)
                {
                    diagnostics.Add(EmitDiagnostics.MissingPlaintextReason(entry.Constant));
                    continue;
                }
            }

            valid.Add(entry);
        }

        return valid;
    }

    private static List<MqSubscriptionEntry> ValidateSubscriptions(
        MqSubscriptionsSpec spec,
        List<MqMessageEntry> validMessages,
        ImmutableArray<EmitDiagnostic>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var knownMessageTypes = new HashSet<string>(
            validMessages.Select(m => m.MessageType), StringComparer.Ordinal);
        var valid = new List<MqSubscriptionEntry>();

        foreach (var entry in spec.Subscriptions)
        {
            if (!ValidateConstant(entry.Constant, out var constReason))
            {
                diagnostics.Add(EmitDiagnostics.InvalidConstantName(
                    "mq-subscriptions.spec.json", entry.Constant, constReason));
                continue;
            }

            if (!seen.Add(entry.Constant))
            {
                diagnostics.Add(EmitDiagnostics.DuplicateConstant(
                    "mq-subscriptions.spec.json", entry.Constant));
                continue;
            }

            if (!sr_patterns.Contains(entry.Pattern))
            {
                var validList = string.Join(", ",
                    sr_patterns.OrderBy(s => s, StringComparer.Ordinal));
                diagnostics.Add(EmitDiagnostics.UnknownPattern(
                    entry.Constant, entry.Pattern, validList));
                continue;
            }

            if (!knownMessageTypes.Contains(entry.MessageType))
            {
                diagnostics.Add(EmitDiagnostics.UnknownMessageType(
                    entry.Constant, entry.MessageType));
                continue;
            }

            if (entry.TieredRetry is { } retry)
            {
                var anyBadTier = false;
                foreach (var tier in retry.Tiers)
                {
                    if (!TimeSpan.TryParseExact(
                        tier, @"hh\:mm\:ss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _))
                    {
                        diagnostics.Add(EmitDiagnostics.InvalidTieredRetryDuration(
                            entry.Constant, tier));
                        anyBadTier = true;
                    }
                }

                if (anyBadTier) continue;
            }

            valid.Add(entry);
        }

        return valid;
    }

    private static bool ValidateConstant(string value, out string reason)
    {
        if (value.Falsey())
        {
            reason = "empty";
            return false;
        }

        if (!char.IsUpper(value[0]) || !char.IsLetter(value[0]))
        {
            reason = "must start with an uppercase ASCII letter";
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch))
            {
                reason = $"invalid character '{ch}'";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }

    private static string EmitMessagesClass(List<MqMessageEntry> messages)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();

        // Constants class — what [MqPub(MqMessages.X)] consumers see.
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Codegen'd string constants for every message type registered in");
        sb.AppendLine("/// <c>contracts/mq-messages/mq-messages.spec.json</c>.");
        sb.AppendLine("/// Apply via <c>[MqPub(MqMessages.X)]</c> on the message type.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class MqMessages");
        sb.AppendLine("{");
        foreach (var m in messages)
        {
            sb.AppendLine($"    /// <summary>Publisher contract for {m.MessageType}.</summary>");
            sb.AppendLine($"    public const string {m.Constant} = \"{m.Constant}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        // Registry — the runtime resolver looks up by constant.
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Runtime registry mapping every <see cref=\"MqMessages\"/> constant to its");
        sb.AppendLine(
            "/// fully-resolved <see cref=\"MqMessageDescriptor\"/> (exchange, encryption,");
        sb.AppendLine("/// default routing key, message type FQN). Looked up by");
        sb.AppendLine("/// <c>MessageWireResolver</c> via the constant on a type's");
        sb.AppendLine("/// <c>[MqPub]</c> attribute.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class MqMessagesRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Lookup table: constant → descriptor.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyDictionary<string, MqMessageDescriptor> "
            + "ByConstant = new Dictionary<string, MqMessageDescriptor>("
            + "System.StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var m in messages)
        {
            var encReason = m.EncryptionReason is null
                ? "null"
                : "\"" + m.EncryptionReason.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var defaultKey = m.DefaultRoutingKey is null
                ? "null"
                : "\"" + m.DefaultRoutingKey.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

            sb.AppendLine(
                $"        [\"{m.Constant}\"] = new MqMessageDescriptor(");
            sb.AppendLine($"            Constant: \"{m.Constant}\",");
            sb.AppendLine($"            MessageTypeName: \"{m.MessageType}\",");
            sb.AppendLine($"            Exchange: \"{m.Exchange}\",");
            sb.AppendLine($"            ExchangeType: \"{m.ExchangeType}\",");
            sb.AppendLine($"            Encryption: \"{m.Encryption}\",");
            sb.AppendLine($"            EncryptionReason: {encReason},");
            sb.AppendLine($"            DefaultRoutingKey: {defaultKey}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static string EmitSubscriptionsClass(List<MqSubscriptionEntry> subscriptions)
    {
        var sb = new StringBuilder();
        EmitFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_TARGET_NAMESPACE};");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Codegen'd string constants for every subscription registered in");
        sb.AppendLine(
            "/// <c>contracts/mq-subscriptions/mq-subscriptions.spec.json</c>.");
        sb.AppendLine(
            "/// Apply via <c>[MqSub(MqSubscriptions.X)]</c> on a handler class.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class MqSubscriptions");
        sb.AppendLine("{");
        foreach (var s in subscriptions)
        {
            sb.AppendLine(
                $"    /// <summary>Subscription consuming {s.MessageType} via "
                + $"queue '{s.QueueName}' (pattern: {s.Pattern}).</summary>");
            sb.AppendLine($"    public const string {s.Constant} = \"{s.Constant}\";");
            sb.AppendLine();
        }

        sb.AppendLine("}");
        sb.AppendLine();

        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Runtime registry mapping every <see cref=\"MqSubscriptions\"/> constant");
        sb.AppendLine(
            "/// to its fully-resolved <see cref=\"MqSubscriptionDescriptor\"/>.");
        sb.AppendLine(
            "/// Used by the assembly-scan registration to build per-subscriber DI entries.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class MqSubscriptionsRegistry");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>Lookup table: constant → descriptor.</summary>");
        sb.AppendLine(
            "    public static readonly IReadOnlyDictionary<string, MqSubscriptionDescriptor> "
            + "ByConstant = new Dictionary<string, MqSubscriptionDescriptor>("
            + "StringComparer.Ordinal)");
        sb.AppendLine("    {");
        foreach (var s in subscriptions)
        {
            var binding = s.RoutingKeyBinding is null
                ? "\"\""
                : "\"" + s.RoutingKeyBinding.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
            var prefetch = s.Prefetch is { } p ? p.ToString() : "10";
            var idem = s.Idempotency is true ? "true" : "false";
            string retry;
            if (s.TieredRetry is { } r)
            {
                var tiers = string.Join(
                    ", ",
                    r.Tiers.Select(t => $"TimeSpan.Parse(\"{t}\")"));
                retry =
                    $"new TieredRetryDescriptor(Tiers: new[] {{ {tiers} }}, "
                    + $"MaxAttempts: {r.MaxAttempts})";
            }
            else
            {
                retry = "null";
            }

            sb.AppendLine($"        [\"{s.Constant}\"] = new MqSubscriptionDescriptor(");
            sb.AppendLine($"            Constant: \"{s.Constant}\",");
            sb.AppendLine($"            MessageTypeName: \"{s.MessageType}\",");
            sb.AppendLine($"            QueueName: \"{s.QueueName}\",");
            sb.AppendLine($"            Pattern: {_PATTERNS_FQN}.{s.Pattern},");
            sb.AppendLine($"            RoutingKeyBinding: {binding},");
            sb.AppendLine($"            Prefetch: {prefetch},");
            sb.AppendLine($"            Idempotency: {idem},");
            sb.AppendLine($"            TieredRetry: {retry}),");
        }

        sb.AppendLine("    };");
        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitFileHeader(StringBuilder sb)
    {
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine("// <auto-generated>");
        sb.AppendLine(
            "//   Generated by DcsvIo.D2.Messaging.SourceGen from contracts/mq-messages/");
        sb.AppendLine(
            "//   mq-messages.spec.json + contracts/mq-subscriptions/mq-subscriptions.spec.json.");
        sb.AppendLine("//   Manual edits will be lost on rebuild.");
        sb.AppendLine("// </auto-generated>");
        sb.AppendLine("// -----------------------------------------------------------------------");
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
    }
}
