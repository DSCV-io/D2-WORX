// -----------------------------------------------------------------------
// <copyright file="SupportedLocales.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n;

using System.Collections.Generic;
using System.Linq;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.Extensions.Configuration;

/// <summary>
/// Defines the set of BCP 47 locales supported by the application and provides
/// helper methods for validation and resolution. Locales are read from
/// <c>PUBLIC_DEFAULT_LOCALE</c> and the indexed env-var section
/// <c>PUBLIC_ENABLED_LOCALES</c> at construction time.
/// </summary>
/// <remarks>
/// <para>
/// All locales are stored in canonical BCP 47 casing (lowercase language,
/// uppercase region — e.g. <c>"en-US"</c>, <c>"fr-CA"</c>).
/// </para>
/// <para>
/// Registered as a DI singleton; captures its config at construction.
/// Nothing mutates after — instances are safe to share across threads and
/// across the application's lifetime.
/// </para>
/// </remarks>
public sealed class SupportedLocales
{
    private const string _DEFAULT_LOCALE_KEY = "PUBLIC_DEFAULT_LOCALE";
    private const string _ENABLED_LOCALES_KEY = "PUBLIC_ENABLED_LOCALES";
    private const string _DEFAULT_BASE_LOCALE = "en-US";

    /// <summary>
    /// Initializes a new instance of the <see cref="SupportedLocales"/> class
    /// from the supplied configuration.
    /// </summary>
    /// <param name="configuration">
    /// The application configuration. <c>PUBLIC_DEFAULT_LOCALE</c> sets
    /// <see cref="Base"/>; <c>PUBLIC_ENABLED_LOCALES__N</c> indexed entries
    /// populate <see cref="All"/>. Both default to <c>"en-US"</c> when absent.
    /// </param>
    public SupportedLocales(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var defaultLocale = configuration[_DEFAULT_LOCALE_KEY];
        Base = defaultLocale.Falsey()
            ? _DEFAULT_BASE_LOCALE
            : ToBcp47(defaultLocale!.Trim());

        var section = configuration.GetSection(_ENABLED_LOCALES_KEY);
        var values = section.GetChildren()
            .Select(c => c.Value)
            .Where(v => !v.Falsey())
            .Select(v => ToBcp47(v!.Trim()))
            .ToList();

        All = values.Truthy() ? values.AsReadOnly() : [_DEFAULT_BASE_LOCALE];

        // Build language-prefix → first-locale map. First locale wins per language.
        var langDefaults = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var locale in All)
        {
            var dashIndex = locale.IndexOf('-');
            var lang = dashIndex > 0 ? locale[..dashIndex] : locale;
            langDefaults.TryAdd(lang, locale);
        }

        LanguageDefaults = langDefaults;
    }

    /// <summary>
    /// Gets the base (default / fallback) locale. Configurable via
    /// <c>PUBLIC_DEFAULT_LOCALE</c>; defaults to <c>"en-US"</c>.
    /// </summary>
    public string Base { get; }

    /// <summary>
    /// Gets all supported locale codes in canonical BCP 47 casing.
    /// </summary>
    public IReadOnlyList<string> All { get; }

    /// <summary>
    /// Gets a map of each language prefix to the first locale for that language.
    /// For example, <c>"en" → "en-US"</c>, <c>"fr" → "fr-FR"</c>.
    /// </summary>
    public IReadOnlyDictionary<string, string> LanguageDefaults { get; }

    /// <summary>
    /// Normalizes any locale tag to canonical BCP 47 casing: lowercase language,
    /// uppercase region (e.g. <c>"en-us"</c> → <c>"en-US"</c>,
    /// <c>"FR-CA"</c> → <c>"fr-CA"</c>). Bare language codes are lowercased
    /// (e.g. <c>"EN"</c> → <c>"en"</c>).
    /// </summary>
    /// <param name="tag">The locale tag to normalize.</param>
    /// <returns>The tag in canonical BCP 47 casing.</returns>
    public static string ToBcp47(string tag)
    {
        ArgumentNullException.ThrowIfNull(tag);

        var dash = tag.IndexOf('-');
        return dash < 0
            ? tag.ToLowerInvariant()
            : tag[..dash].ToLowerInvariant() + tag[dash..].ToUpperInvariant();
    }

    /// <summary>
    /// Determines whether the given locale code is supported. Input is normalized
    /// to canonical BCP 47 casing before comparison.
    /// </summary>
    /// <param name="locale">The locale code to check.</param>
    /// <returns>
    /// <see langword="true"/> if the locale is in the supported list;
    /// otherwise <see langword="false"/>.
    /// </returns>
    public bool IsValid(string locale) => All.Contains(ToBcp47(locale));

    /// <summary>
    /// Resolves a locale code to its canonical BCP 47 form. Resolution order:
    /// canonical match → language-prefix fallback (first registered locale of
    /// the same language family) → <see cref="Base"/>. Null / empty / whitespace
    /// input collapses to <see cref="Base"/>.
    /// </summary>
    /// <param name="locale">The locale code to resolve, or <see langword="null"/>.</param>
    /// <returns>A supported locale code in canonical BCP 47 casing.</returns>
    public string Resolve(string? locale)
    {
        if (locale.Falsey())
        {
            return Base;
        }

        var canonical = ToBcp47(locale!.Trim());

        if (All.Contains(canonical))
        {
            return canonical;
        }

        // Language-prefix fallback: "fr-CH" → "fr-FR" (the first fr-* in r_all).
        var dash = canonical.IndexOf('-');
        var lang = dash > 0 ? canonical[..dash] : canonical;
        return LanguageDefaults.GetValueOrDefault(lang, Base);
    }
}
