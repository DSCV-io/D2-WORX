// -----------------------------------------------------------------------
// <copyright file="InputError.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result;

using System.Text.Json.Serialization;
using DcsvIo.D2.I18n;

/// <summary>
/// A field-level validation error: the offending field name plus one or more
/// translation messages describing what's wrong with that field.
/// </summary>
/// <remarks>
/// <para>
/// Wire format is a self-describing object —
/// <c>{ "field": "email", "errors": [{ "key": "..." }] }</c>.
/// Self-describing keys are easier to extend (e.g. adding a per-error
/// <c>severity</c> field later) and don't depend on positional indexing at the
/// consumer.
/// </para>
/// <para>
/// The property names (<c>field</c>, <c>errors</c>) come from the spec-derived
/// <see cref="InputErrorWireShape"/> catalog —
/// <c>contracts/input-error/input-error.spec.json</c> drives both the .NET
/// serializer and the TS-side parser, so cross-language wire drift on the
/// property names is structurally impossible. The
/// <c>[JsonPropertyName(InputErrorWireShape.*)]</c> attributes on the
/// record parameters wire the camelCase wire names directly onto the
/// PascalCase code-side properties — so the serialization is wire-correct
/// regardless of whether the call site passes the SR_Web options.
/// </para>
/// <para>
/// Each entry in <see cref="Errors"/> is a <see cref="TKMessage"/>, so the type
/// system enforces "field-error messages are translation keys" identically to
/// the top-level <c>D2Result.Messages</c> contract.
/// </para>
/// </remarks>
/// <param name="Field">The name of the offending input field (e.g. <c>"email"</c>).</param>
/// <param name="Errors">
/// One or more translatable error messages describing what's wrong with the field.
/// </param>
public sealed record InputError(
    [property: JsonPropertyName(InputErrorWireShape.FIELD)] string Field,
    [property: JsonPropertyName(InputErrorWireShape.ERRORS)] IReadOnlyList<TKMessage> Errors);
