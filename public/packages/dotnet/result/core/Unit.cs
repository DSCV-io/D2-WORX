// -----------------------------------------------------------------------
// <copyright file="Unit.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

/// <summary>
/// Singleton "no value" type for handlers / operations whose success state
/// carries no meaningful payload. Use as the <c>TOutput</c> in
/// <c>D2Result&lt;Unit&gt;</c> when "succeeded" is the only thing worth
/// communicating (subscribers, fire-and-forget commands, etc.).
/// </summary>
/// <remarks>
/// Equivalent to <c>void</c> but generic-typeable. Functional languages
/// call this <c>Unit</c>; F# / LanguageExt / many .NET libraries use the
/// same name.
/// </remarks>
public readonly record struct Unit
{
    /// <summary>Gets the single instance.</summary>
    public static Unit Value => default;
}
