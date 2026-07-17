// -----------------------------------------------------------------------
// <copyright file="RedactDataDestructuringPolicy.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Destructuring;

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using DcsvIo.D2.Utilities.Attributes;
using Serilog.Core;
using Serilog.Events;

/// <summary>
/// Serilog destructuring policy that processes
/// <see cref="RedactDataAttribute"/> on types and properties, replacing
/// annotated values with a redaction placeholder in destructured log output.
/// </summary>
/// <remarks>
/// <para>Reflection results are cached per type via a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/> so that repeated
/// destructuring of the same type incurs no repeated reflection cost.</para>
/// <para>
/// Type-level <c>[RedactData]</c> → entire value replaced with
/// <c>[REDACTED: {Reason}]</c>.
/// </para>
/// <para>
/// Property-level <c>[RedactData]</c> → annotated properties masked with the
/// same placeholder; non-annotated properties destructured normally via
/// <see cref="ILogEventPropertyValueFactory.CreatePropertyValue(object, bool)"/>
/// (which re-enters this policy if the nested value also carries
/// <c>[RedactData]</c>, so collections of redacted types and nested
/// redacted-typed properties are handled transparently by recursion).
/// </para>
/// <para>
/// Limitation: the policy reflects over <c>BindingFlags.Public | Instance</c>
/// PROPERTIES — fields are NOT inspected, so <c>[RedactData]</c> on a field
/// is silently ignored. Use property syntax for redaction.
/// </para>
/// <para>
/// Limitation: redaction operates on STRUCTURAL destructuring only — Serilog
/// captures with the <c>@</c> operator (e.g. <c>logger.Information("{@User}", user)</c>)
/// invoke this policy. Captures without <c>@</c> (e.g. <c>"{User}"</c>) call
/// <c>.ToString()</c> on the value and bypass destructuring entirely; redaction
/// does not apply on that path. Discipline is required at the call site.
/// </para>
/// </remarks>
internal sealed class RedactDataDestructuringPolicy : IDestructuringPolicy
{
    // Instance-scoped so each policy instance (one per Serilog LoggerConfiguration in
    // production; one per `new RedactDataDestructuringPolicy()` in tests) has its own
    // cache.  In production Serilog creates exactly one policy instance per pipeline, so
    // the performance characteristics are identical to the previous static cache.  The
    // instance field eliminates the shared-static race between test classes that call
    // ClearCache() and assert exact CacheCount values in parallel.
    private readonly ConcurrentDictionary<Type, TypeRedactionInfo> r_cache = new();

    /// <summary>
    /// Gets the count of analyzed types currently held in the per-instance cache.
    /// Internal hook for unit tests that pin cache-hit + thread-safety
    /// behavior. Not intended for production consumers.
    /// </summary>
    internal int CacheCount => r_cache.Count;

    /// <inheritdoc />
    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        [NotNullWhen(true)] out LogEventPropertyValue? result)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(propertyValueFactory);

        var type = value.GetType();
        var info = r_cache.GetOrAdd(type, AnalyzeType);

        // Type-level [RedactData] → replace entire value with placeholder.
        if (info.TypeRedactionReason is not null)
        {
            result = new ScalarValue($"[REDACTED: {info.TypeRedactionReason}]");
            return true;
        }

        // No property-level redactions → let Serilog handle normally.
        if (info.PropertyRedactions.Count == 0)
        {
            result = null;
            return false;
        }

        // Build StructureValue with masked properties, recursing for the rest.
        var properties = new List<LogEventProperty>(info.AllProperties.Length);
        foreach (var prop in info.AllProperties)
        {
            if (info.PropertyRedactions.TryGetValue(prop.Name, out var reason))
            {
                properties.Add(new LogEventProperty(
                    prop.Name,
                    new ScalarValue($"[REDACTED: {reason}]")));
            }
            else
            {
                var propValue = prop.GetValue(value);
                properties.Add(new LogEventProperty(
                    prop.Name,
                    propertyValueFactory.CreatePropertyValue(propValue, destructureObjects: true)));
            }
        }

        result = new StructureValue(properties, type.Name);
        return true;
    }

    /// <summary>
    /// Clears the per-instance analysis cache so a subsequent
    /// <see cref="TryDestructure"/> call repopulates it. Internal hook for
    /// unit tests that pin cache behavior. Not intended for production
    /// consumers.
    /// </summary>
    internal void ClearCache() => r_cache.Clear();

    private static TypeRedactionInfo AnalyzeType(Type type)
    {
        // Type-level attribute walks the inheritance chain by default
        // (GetCustomAttribute<T>() with inherit=true is the BCL default).
        var typeAttr = type.GetCustomAttribute<RedactDataAttribute>();
        if (typeAttr is not null)
        {
            var reason = typeAttr.CustomReason ?? typeAttr.Reason.ToString();
            return new TypeRedactionInfo(
                reason,
                AllProperties: [],
                PropertyRedactions: new Dictionary<string, string>(0));
        }

        var allProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var redactions = new Dictionary<string, string>(allProps.Length, StringComparer.Ordinal);
        foreach (var prop in allProps)
        {
            var propAttr = prop.GetCustomAttribute<RedactDataAttribute>();
            if (propAttr is not null)
            {
                var reason = propAttr.CustomReason ?? propAttr.Reason.ToString();
                redactions[prop.Name] = reason;
            }
        }

        return new TypeRedactionInfo(
            TypeRedactionReason: null,
            AllProperties: allProps,
            PropertyRedactions: redactions);
    }
}
