// -----------------------------------------------------------------------
// <copyright file="MqSubAttribute.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging;

using System;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Marks a handler class as a messaging subscriber. Carries one
/// <c>MqSubscriptions</c> constant; that constant resolves at runtime
/// (via <c>MqSubscriptionsRegistry</c>) to the full subscription contract:
/// queue name, pattern, routing-key binding, prefetch, idempotency,
/// optional retry tiers.
/// </summary>
/// <remarks>
/// <para>
/// Apply on a class deriving from
/// <c>BaseHandler&lt;TSelf, TMessage, Unit&gt;</c>. The assembly-scan
/// registration (<c>services.AddD2SubscribersFromAssembly</c>) finds
/// every class with this attribute, validates the constraint, looks up
/// the subscription descriptor, and registers the handler + a backing
/// <see cref="ISubscriberRegistration"/> automatically.
/// </para>
/// <para>
/// The descriptor's <c>MessageTypeName</c> MUST match the handler's
/// <c>TMessage</c> generic parameter (validated at registration time).
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class MqSubAttribute : Attribute
{
    /// <summary>Initializes a new instance of the <see cref="MqSubAttribute"/>
    /// class with the given <c>MqSubscriptions</c> constant.</summary>
    /// <param name="constant">A constant from the codegen-emitted
    /// <c>MqSubscriptions</c> static class (e.g.
    /// <c>MqSubscriptions.KeyringRefresh</c>). Used as the lookup key into
    /// <c>MqSubscriptionsRegistry</c>.</param>
    public MqSubAttribute(string constant)
    {
        constant.ThrowIfFalsey();
        Constant = constant;
    }

    /// <summary>Gets the <c>MqSubscriptions</c> constant identifying this
    /// handler's subscription contract.</summary>
    public string Constant { get; }
}
