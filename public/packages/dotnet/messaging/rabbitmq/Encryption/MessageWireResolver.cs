// -----------------------------------------------------------------------
// <copyright file="MessageWireResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.RabbitMq.Encryption;

using System;
using System.Collections.Concurrent;
using System.Reflection;
using DcsvIo.D2.Messaging;

/// <summary>
/// Resolves a message <see cref="Type"/> to its <see cref="MqMessageDescriptor"/>
/// via the type's <see cref="MqPubAttribute"/> + the codegen-emitted
/// <c>MqMessagesRegistry</c>. Per-type cached so per-publish overhead is one
/// dictionary lookup.
/// </summary>
/// <remarks>
/// <para>
/// Default-deny: a message type without <see cref="MqPubAttribute"/> throws
/// <see cref="InvalidOperationException"/> at first publish. A type with
/// <see cref="MqPubAttribute"/> referencing an unknown constant (i.e. not
/// in the codegen'd <c>MqMessagesRegistry</c>) also throws — likely caused
/// by a stale build after a spec edit.
/// </para>
/// <para>
/// The single <see cref="MqPubAttribute"/> encodes both the exchange +
/// the encryption decision (via the spec entry's <c>encryption</c> field).
/// </para>
/// </remarks>
internal static class MessageWireResolver
{
    private static readonly ConcurrentDictionary<Type, MqMessageDescriptor> sr_cache = new();

    /// <summary>
    /// Types seeded via <see cref="RegisterForTesting"/>. Survives the default
    /// <see cref="ClearCache"/> path so parallel unit tests cannot wipe
    /// integration fixture descriptors mid-delivery (poll-budget flakes).
    /// </summary>
    private static readonly ConcurrentDictionary<Type, MqMessageDescriptor> sr_testPins = new();

    /// <summary>Resolves the <see cref="MqMessageDescriptor"/> for
    /// <paramref name="messageType"/> via the production
    /// <c>MqMessagesRegistry.ByConstant</c>. Throws if the type is missing
    /// <see cref="MqPubAttribute"/>, references an unknown constant, or its
    /// FQN doesn't match the registered descriptor.</summary>
    /// <param name="messageType">The message CLR type.</param>
    public static MqMessageDescriptor Resolve(Type messageType) =>
        Resolve(messageType, MqMessagesRegistry.ByConstant);

    /// <summary>Test seam — same lookup logic but against an injected registry.
    /// Production code never calls this overload directly; tests pass an
    /// in-memory dictionary so synthetic types don't need entries in the
    /// production spec file.</summary>
    /// <param name="messageType">The message CLR type.</param>
    /// <param name="registry">The constant → descriptor map.</param>
    internal static MqMessageDescriptor Resolve(
        Type messageType, IReadOnlyDictionary<string, MqMessageDescriptor> registry)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(registry);

        // Cache by type only — production resolves against the immutable
        // codegen'd registry, so the same type always resolves to the same
        // descriptor. Tests that pass alternate registries should clear via
        // ClearCache() between cases.
        return sr_cache.GetOrAdd(messageType, t => ResolveCore(t, registry));
    }

    /// <summary>
    /// Test-only cache reset. Production never calls this.
    /// </summary>
    /// <param name="includeTestPins">
    /// When <c>false</c> (default), production-resolved cache entries are
    /// dropped but <see cref="RegisterForTesting"/> pins are restored — so a
    /// unit test clear cannot strand an in-flight Integration host whose
    /// fixture types have no <c>[MqPub]</c>. When <c>true</c>, pins are wiped
    /// too (only for tests that deliberately assert unregistered fixtures).
    /// </param>
    internal static void ClearCache(bool includeTestPins = false)
    {
        if (includeTestPins)
        {
            sr_testPins.Clear();
            sr_cache.Clear();
            return;
        }

        sr_cache.Clear();

        foreach (var kvp in sr_testPins)
            sr_cache[kvp.Key] = kvp.Value;
    }

    /// <summary>Test-only — pre-seeds the cache so an integration test
    /// fixture type whose CLR FQN is NOT in the production
    /// <c>MqMessagesRegistry</c> can still be published / dispatched. The
    /// usual FQN-vs-spec validation is bypassed; the supplied descriptor is
    /// returned verbatim for subsequent <see cref="Resolve(Type)"/> calls.
    /// Production code never calls this.</summary>
    /// <param name="messageType">The fixture's CLR type.</param>
    /// <param name="descriptor">The descriptor to register.</param>
    internal static void RegisterForTesting(
        Type messageType, MqMessageDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        ArgumentNullException.ThrowIfNull(descriptor);
        sr_testPins[messageType] = descriptor;
        sr_cache[messageType] = descriptor;
    }

    private static MqMessageDescriptor ResolveCore(
        Type messageType, IReadOnlyDictionary<string, MqMessageDescriptor> registry)
    {
        var attr = messageType.GetCustomAttribute<MqPubAttribute>(inherit: false)
            ?? throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' has no [MqPub] attribute. "
                + $"Apply [MqPub(MqMessages.YourConstant)] on the type's companion "
                + $"partial class. Add a corresponding entry to "
                + $"contracts/mq-messages/mq-messages.spec.json. The publisher's "
                + $"default-deny posture means a forgotten attribute fails loud "
                + $"rather than silently routing to an unintended exchange.");

        if (!registry.TryGetValue(attr.Constant, out var descriptor))
        {
            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' carries "
                + $"[MqPub(\"{attr.Constant}\")] but '{attr.Constant}' is not "
                + $"in the codegen'd MqMessagesRegistry. Likely causes: stale "
                + $"build after spec edit, typo in the constant reference, or "
                + $"the spec entry was renamed without updating the attribute. "
                + $"Rebuild the solution; if the error persists, verify the "
                + $"constant name in contracts/mq-messages/mq-messages.spec.json.");
        }

        if (!string.Equals(
            descriptor.MessageTypeName, messageType.FullName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Message type '{messageType.FullName}' carries "
                + $"[MqPub(\"{attr.Constant}\")] but the spec entry's "
                + $"messageType is '{descriptor.MessageTypeName}'. The CLR type "
                + $"name and the spec messageType must match exactly. Either "
                + $"rename the type or update the spec entry.");
        }

        return descriptor;
    }
}
