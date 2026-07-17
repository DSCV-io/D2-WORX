// -----------------------------------------------------------------------
// <copyright file="AudienceEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Auth.Audiences.SourceGen;

/// <summary>
/// One audience entry parsed from the spec.
/// </summary>
/// <param name="Name">
/// PascalCase identifier emitted as a <c>public const string</c> on the
/// generated <c>Audiences</c> class (e.g. <c>"Files"</c> →
/// <c>Audiences.Files</c>). Must match <c>^[A-Z][A-Za-z0-9]*$</c>.
/// </param>
/// <param name="Url">
/// Absolute URI used as the JWT <c>aud</c> claim value AND as the
/// <c>targetAudience</c> argument to <c>TokenExchangeClient.ExchangeAsync</c>.
/// The const's value is this URL string. Must parse as an absolute
/// <see cref="System.Uri"/>.
/// </param>
/// <param name="Description">
/// Free-form description rendered as XML doc on the emitted constant.
/// </param>
internal sealed record AudienceEntry(string Name, string Url, string? Description);
