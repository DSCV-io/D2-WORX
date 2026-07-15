// -----------------------------------------------------------------------
// <copyright file="MqPubAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Marks a message type as publishable through <see cref="IMessageBus"/>.
/// Carries one <c>MqMessages</c> constant; that constant resolves at
/// runtime (via <c>MqMessagesRegistry</c>) to the full publisher contract:
/// target exchange, exchange type, encryption domain, default routing key.
/// </summary>
/// <remarks>
/// <para>
/// Every message type a service publishes MUST carry this attribute. The
/// publisher's resolver throws <see cref="InvalidOperationException"/> on
/// the first publish of a type that lacks it — default-deny by design (a
/// silently-routed message is the worst possible failure mode for
/// configuration drift).
/// </para>
/// <para>
/// Apply directly on the message type — for proto-generated messages, on the
/// companion partial class (the generated source does not carry it); for
/// hand-written message records, on the record itself.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MqPubAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MqPubAttribute"/>
    /// class with the given <c>MqMessages</c> constant.</summary>
    /// <param name="constant">A constant from the codegen-emitted
    /// <c>MqMessages</c> static class (e.g. <c>MqMessages.AuthKeyRotated</c>).
    /// Used as the lookup key into <c>MqMessagesRegistry</c>.</param>
    public MqPubAttribute(string constant)
    {
        constant.ThrowIfFalsey();
        Constant = constant;
    }

    /// <summary>Gets the <c>MqMessages</c> constant identifying this message
    /// type's publisher contract.</summary>
    public string Constant { get; }
}
