// -----------------------------------------------------------------------
// <copyright file="InputError.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Result;

using D2.Shared.I18n;

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
/// Each entry in <see cref="Errors"/> is a <see cref="TKMessage"/>, so the type
/// system enforces "field-error messages are translation keys" identically to
/// the top-level <c>D2Result.Messages</c> contract.
/// </para>
/// </remarks>
/// <param name="Field">The name of the offending input field (e.g. <c>"email"</c>).</param>
/// <param name="Errors">
/// One or more translatable error messages describing what's wrong with the field.
/// </param>
public sealed record InputError(string Field, IReadOnlyList<TKMessage> Errors);
