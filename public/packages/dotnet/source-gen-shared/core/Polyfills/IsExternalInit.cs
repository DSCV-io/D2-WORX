// -----------------------------------------------------------------------
// <copyright file="IsExternalInit.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

// ReSharper disable once CheckNamespace -- polyfill MUST live in
// System.Runtime.CompilerServices for the C# compiler to recognize it
// as the synthesized init-accessor marker. Folder location is irrelevant.
namespace System.Runtime.CompilerServices;

using System.ComponentModel;

/// <summary>
/// Polyfill enabling C# 9 <c>init</c> accessors on records when targeting
/// netstandard2.0. The runtime ignores the marker; the compiler synthesizes
/// it into init-only setters when emitting record types. Required because
/// Roslyn analyzers / source generators MUST target netstandard2.0 and
/// netstandard2.0 does not ship this type. Wired into each source-gen
/// csproj via the shared <c>Compile Include</c> from
/// <c>source-gen-shared/</c>.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
