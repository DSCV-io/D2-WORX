// -----------------------------------------------------------------------
// <copyright file="ITranslator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n;

/// <summary>
/// Resolves a <see cref="TKMessage"/> to a localized, parameter-substituted
/// string for the specified locale.
/// </summary>
/// <remarks>
/// <para>
/// The dominant translation boundary is client-side: the SvelteKit / Paraglide
/// frontend consumes <see cref="TKMessage"/> objects shipped over the wire and
/// renders them. This server-side <see cref="ITranslator"/> exists only for
/// outbound notification rendering (Courier emails / SMS / push) where the
/// recipient's preferred locale is known from their user profile and the
/// rendered text must be inlined into the notification payload before delivery.
/// </para>
/// <para>
/// Implementations MUST be thread-safe — the translator is registered as a
/// process-wide singleton and called concurrently from multiple notification
/// handlers.
/// </para>
/// </remarks>
public interface ITranslator
{
    /// <summary>
    /// Translates the supplied <paramref name="message"/> for the specified
    /// <paramref name="locale"/>, substituting any bound parameters.
    /// </summary>
    /// <param name="locale">
    /// The BCP 47 locale code (e.g. <c>"en-US"</c>, <c>"fr-FR"</c>). Resolved
    /// to a supported locale via the implementation's locale resolution chain
    /// (canonical match → language-family fallback → base locale).
    /// </param>
    /// <param name="message">
    /// The message to translate, comprising a translation key and optional
    /// parameter bindings.
    /// </param>
    /// <returns>
    /// The localized, parameter-substituted string. Falls back to the base
    /// locale's translation if the requested locale lacks the key, then to
    /// the raw key string itself if no translation exists at all (the
    /// raw-key fallback ensures translation never throws — keys are
    /// dev-readable identifiers that serve as a useful debugging signal).
    /// </returns>
    string T(string locale, TKMessage message);

    /// <summary>
    /// Determines whether the specified <paramref name="key"/> exists in any
    /// loaded locale catalog. Useful for boundary code (e.g. wire-format
    /// parsers that received an arbitrary string and want to know whether
    /// it's a known translation key before treating it as one).
    /// </summary>
    /// <param name="key">
    /// The translation key to check (the raw string form, not a <see cref="TKMessage"/>).
    /// </param>
    /// <returns>
    /// <see langword="true"/> if the key is known; otherwise <see langword="false"/>.
    /// </returns>
    bool HasKey(string key);
}
