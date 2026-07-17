// -----------------------------------------------------------------------
// <copyright file="TypeRedactionInfo.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Logging.Destructuring;

using System.Reflection;

/// <summary>
/// Cached reflection analysis result for a single type, used by
/// <see cref="RedactDataDestructuringPolicy"/> to avoid repeating the
/// <c>GetCustomAttribute</c> + <c>GetProperties</c> walk on every
/// destructure call for the same type.
/// </summary>
/// <param name="TypeRedactionReason">
/// When non-null, the entire instance is replaced by
/// <c>[REDACTED: {TypeRedactionReason}]</c> in log output (type-level
/// redaction). When null, individual properties are masked per
/// <paramref name="PropertyRedactions"/>.
/// </param>
/// <param name="AllProperties">
/// All public instance properties of the type, captured once via reflection
/// and reused for every destructure of an instance of the type.
/// </param>
/// <param name="PropertyRedactions">
/// Map of property name → redaction reason for each property carrying
/// <c>[RedactData]</c>. Empty when no property-level redactions exist.
/// </param>
internal sealed record TypeRedactionInfo(
    string? TypeRedactionReason,
    PropertyInfo[] AllProperties,
    IReadOnlyDictionary<string, string> PropertyRedactions);
