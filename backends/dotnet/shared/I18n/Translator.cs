// -----------------------------------------------------------------------
// <copyright file="Translator.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.I18n;

using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Loads JSON message catalogs from a directory and provides locale-aware
/// translation with parameter interpolation.
/// </summary>
public partial class Translator : ITranslator
{
    private const string _SCHEMA_KEY = "$schema";

    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Matches <c>{paramName}</c> placeholders in translation strings.
    /// </summary>
    private static readonly Regex sr_paramPattern = ParamRegex();

    /// <summary>
    /// Locale code -> (key -> translated string). Outer key is the locale filename stem
    /// in canonical BCP 47 casing (e.g. "en-US"). <see cref="SupportedLocales.Resolve"/>
    /// also returns canonical casing, so lookups are direct string matches.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> r_catalogs = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="Translator"/> class by loading
    /// all <c>*.json</c> message catalogs from the specified directory.
    /// </summary>
    /// <param name="messagesDirectory">
    /// The absolute path to the directory containing locale JSON files
    /// (e.g. <c>contracts/messages/</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="messagesDirectory"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the specified directory does not exist.
    /// </exception>
    public Translator(string messagesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(messagesDirectory);

        if (!Directory.Exists(messagesDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Messages directory not found: {messagesDirectory}");
        }

        LoadCatalogs(messagesDirectory);
    }

    /// <inheritdoc/>
    public string T(string locale, string key, Dictionary<string, string>? parameters = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var resolvedLocale = SupportedLocales.Resolve(locale);

        // Try requested locale first
        if (r_catalogs.TryGetValue(resolvedLocale, out var catalog) &&
            catalog.TryGetValue(key, out var value))
        {
            return Interpolate(value, parameters);
        }

        // Fall back to base locale
        if (resolvedLocale != SupportedLocales.Base &&
            r_catalogs.TryGetValue(SupportedLocales.Base, out var baseCatalog) &&
            baseCatalog.TryGetValue(key, out var baseValue))
        {
            return Interpolate(baseValue, parameters);
        }

        // No translation found — return the raw key
        return key;
    }

    /// <inheritdoc/>
    public bool HasKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        foreach (var catalog in r_catalogs.Values)
        {
            if (catalog.ContainsKey(key))
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.None, matchTimeoutMilliseconds: 100)]
    private static partial Regex ParamRegex();

    /// <summary>
    /// Replaces <c>{paramName}</c> placeholders in the template with values from
    /// the parameters dictionary. Unmatched placeholders are left as-is.
    /// </summary>
    private static string Interpolate(string template, Dictionary<string, string>? parameters)
    {
        if (parameters is null || parameters.Count == 0)
        {
            return template;
        }

        return sr_paramPattern.Replace(template, match =>
        {
            var paramName = match.Groups[1].Value;
            return parameters.TryGetValue(paramName, out var replacement)
                ? replacement
                : match.Value;
        });
    }

    /// <summary>
    /// Loads all <c>*.json</c> files from the specified directory into the catalog dictionary.
    /// Each file's name (without extension) is treated as the locale code.
    /// The <c>$schema</c> key is skipped.
    /// </summary>
    private void LoadCatalogs(string directory)
    {
        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json"))
        {
            var locale = Path.GetFileNameWithoutExtension(filePath);
            var json = File.ReadAllText(filePath);

            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(json, sr_jsonOptions);

            if (entries is null)
            {
                continue;
            }

            entries.Remove(_SCHEMA_KEY);
            r_catalogs[SupportedLocales.ToBcp47(locale)] = entries;
        }
    }
}
