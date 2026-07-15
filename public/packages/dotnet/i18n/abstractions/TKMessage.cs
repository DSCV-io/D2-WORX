// -----------------------------------------------------------------------
// <copyright file="TKMessage.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// A translatable message: a translation key plus an optional dictionary of
/// parameters bound for substitution into the translated template at the
/// translation boundary (typically the SvelteKit BFF / browser via Paraglide,
/// or — for outbound notifications — the server-side <see cref="ITranslator"/>).
/// </summary>
/// <remarks>
/// <para>
/// This is the structural primitive for ALL user-facing translatable strings
/// across the codebase: <c>D2Result.Messages</c>, <c>InputError.Errors</c>,
/// notification template references, etc. Producers construct
/// <see cref="TKMessage"/> instances exclusively via the SrcGen-emitted
/// <c>TK</c> constants (e.g. <c>TK.Common.Errors.NOT_FOUND</c>) and bind
/// parameters via <see cref="With(string, string)"/>.
/// </para>
/// <para>
/// The constructor is <see langword="internal"/> — user code can never
/// synthesize a <see cref="TKMessage"/> from a raw string. This makes
/// "untranslated literal in <c>D2Result.Messages</c>" structurally
/// unrepresentable.
/// </para>
/// <para>
/// Wire format via <see cref="TKMessageJsonConverter"/>: <c>{ "key": "..." }</c>
/// for the no-params case, <c>{ "key": "...", "params": { ... } }</c> when
/// parameters are bound. The same shape is used in code AND on the wire.
/// The property names (<c>key</c>, <c>params</c>) come from the spec-derived
/// <see cref="TkMessageWireShape"/> catalog —
/// <c>contracts/tk-message/tk-message.spec.json</c> drives both the .NET
/// serializer and the TS-side parser, so cross-language wire drift on the
/// property names is structurally impossible.
/// </para>
/// </remarks>
[JsonConverter(typeof(TKMessageJsonConverter))]
public sealed record TKMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TKMessage"/> class with
    /// the specified key and optional parameters.
    /// </summary>
    /// <param name="key">The translation key (e.g. <c>"common_errors_NOT_FOUND"</c>).</param>
    /// <param name="parameters">
    /// Optional parameter bindings for placeholder substitution. Keys are
    /// placeholder names without braces (e.g. <c>"minLength"</c> for
    /// <c>{minLength}</c>); values are the substitution text.
    /// </param>
    /// <remarks>
    /// Internal so user code can only obtain a <see cref="TKMessage"/> via
    /// the SrcGen-emitted <c>TK</c> constants. The <see cref="TKMessageJsonConverter"/>
    /// uses this constructor on deserialization.
    /// </remarks>
    internal TKMessage(string key, IReadOnlyDictionary<string, string>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Parameters = parameters;
    }

    /// <summary>
    /// Gets the translation key. Matches a key in <c>contracts/messages/{locale}.json</c>.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the optional parameter bindings for placeholder substitution.
    /// <see langword="null"/> when no parameters are bound.
    /// </summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; }

    /// <summary>
    /// Returns a new <see cref="TKMessage"/> with the same key and the
    /// specified single parameter binding added (or replacing the existing
    /// binding for that name).
    /// </summary>
    /// <param name="name">The placeholder name (without braces).</param>
    /// <param name="value">The substitution value.</param>
    /// <returns>A new <see cref="TKMessage"/>; the receiver is unchanged.</returns>
    public TKMessage With(string name, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(value);

        var combined = Parameters is null
            ? new Dictionary<string, string>(capacity: 1) { [name] = value }
            : new Dictionary<string, string>(Parameters) { [name] = value };
        return new TKMessage(Key, combined);
    }

    /// <summary>
    /// Returns a new <see cref="TKMessage"/> with the same key and the
    /// specified parameter dictionary replacing any existing bindings.
    /// </summary>
    /// <param name="parameters">The full parameter binding map.</param>
    /// <returns>A new <see cref="TKMessage"/>; the receiver is unchanged.</returns>
    public TKMessage With(IReadOnlyDictionary<string, string> parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new TKMessage(Key, parameters);
    }

    /// <summary>
    /// Determines whether the specified <see cref="TKMessage"/> is equal to
    /// this instance. Two messages are equal when their <see cref="Key"/>
    /// values match AND their <see cref="Parameters"/> dictionaries contain
    /// the same key/value pairs (order-independent).
    /// </summary>
    /// <param name="other">The other message to compare against.</param>
    /// <returns><see langword="true"/> if equal; otherwise <see langword="false"/>.</returns>
    public bool Equals(TKMessage? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!string.Equals(Key, other.Key, StringComparison.Ordinal))
        {
            return false;
        }

        return ParametersEqual(Parameters, other.Parameters);
    }

    /// <summary>
    /// Gets a hash code combining <see cref="Key"/> and the parameter bindings
    /// (order-independent — sums each entry's combined hash).
    /// </summary>
    /// <returns>A hash code consistent with <see cref="Equals(TKMessage)"/>.</returns>
    public override int GetHashCode()
    {
        var hash = Key.GetHashCode(StringComparison.Ordinal);
        if (Parameters is null)
        {
            return hash;
        }

        var paramsHash = 0;
        foreach (var kvp in Parameters)
        {
            paramsHash += HashCode.Combine(
                kvp.Key.GetHashCode(StringComparison.Ordinal),
                kvp.Value.GetHashCode(StringComparison.Ordinal));
        }

        return HashCode.Combine(hash, paramsHash);
    }

    private static bool ParametersEqual(
        IReadOnlyDictionary<string, string>? a,
        IReadOnlyDictionary<string, string>? b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        foreach (var kvp in a)
        {
            if (!b.TryGetValue(kvp.Key, out var bValue) ||
                !string.Equals(kvp.Value, bValue, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
