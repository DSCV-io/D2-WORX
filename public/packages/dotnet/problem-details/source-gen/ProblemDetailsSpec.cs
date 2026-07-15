// -----------------------------------------------------------------------
// <copyright file="ProblemDetailsSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ProblemDetails.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of
/// <c>contracts/problem-details/problem-details.spec.json</c>.
/// The <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="ProblemDetailsEmitter"/>.
/// </summary>
/// <param name="TypeUriPrefix">
/// Base URI for the RFC 7807 ProblemDetails <c>type</c> field. The runtime
/// appends the kebab-cased error code directly; must end with a trailing
/// slash (validated by D2PRB006).
/// </param>
/// <param name="ContentType">
/// MIME type emitted on responses carrying an RFC 7807 ProblemDetails body
/// (per RFC 7807 §6.1: <c>application/problem+json</c> for JSON bodies).
/// </param>
/// <param name="ExtensionKeys">
/// Every extension-key entry declared in the spec (in spec order).
/// </param>
/// <param name="Titles">
/// Every title entry declared in the spec (in spec order).
/// </param>
internal sealed record ProblemDetailsSpec(
    string TypeUriPrefix,
    string ContentType,
    ImmutableArray<ExtensionKeyEntry> ExtensionKeys,
    ImmutableArray<TitleEntry> Titles);
