// -----------------------------------------------------------------------
// <copyright file="MqMessagesFixtureEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Integration.ContractFixtures;

using System;
using System.Collections.Generic;
using DcsvIo.D2.Messaging;
using Xunit;

/// <summary>
/// Emits the <c>mq-messages</c> descriptor-mirror parity fixture from the
/// runtime <see cref="MqMessagesRegistry"/> so the TS
/// <c>@dcsv-io/d2-messaging-abstractions</c> <c>MqMessagesRegistry</c> mirror (emitted
/// by <c>tools/ts-codegen/src/mq-messages-emit.ts</c> from the same
/// <c>contracts/mq-messages/mq-messages.spec.json</c>) can be asserted
/// field-by-field against the .NET source of truth. Field names use the TS
/// (camelCase) descriptor shape; a null <c>encryptionReason</c> /
/// <c>defaultRoutingKey</c> is omitted to match the TS <c>?:</c> omit-when-absent
/// posture.
/// </summary>
public sealed class MqMessagesFixtureEmitter
{
    private const string CATALOG = "mq-messages";

    [Fact]
    [Trait("Category", "ContractFixtures")]
    public void Emit_Registry()
    {
        var data = new SortedDictionary<string, object?>(StringComparer.Ordinal);

        foreach (var kvp in MqMessagesRegistry.ByConstant)
        {
            var d = kvp.Value;

            var entry = new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["constant"] = d.Constant,
                ["messageType"] = d.MessageTypeName,
                ["exchange"] = d.Exchange,
                ["exchangeType"] = d.ExchangeType,
                ["encryption"] = d.Encryption,
            };

            if (d.EncryptionReason is not null)
                entry["encryptionReason"] = d.EncryptionReason;

            if (d.DefaultRoutingKey is not null)
                entry["defaultRoutingKey"] = d.DefaultRoutingKey;

            data[kvp.Key] = entry;
        }

        FixturePathHelpers.WriteFixture(CATALOG, "registry", data);
    }
}
