// -----------------------------------------------------------------------
// <copyright file="Translator.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.I18n;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using DcsvIo.D2.Utilities.Extensions;

/// <summary>
/// Loads JSON message catalogs from a directory and resolves
/// <see cref="TKMessage"/> instances to localized, parameter-substituted strings.
/// </summary>
/// <remarks>
/// <para>
/// Most translation happens client-side via the SvelteKit / Paraglide
/// frontend consuming <see cref="TKMessage"/> objects shipped over the wire.
/// This server-side <see cref="Translator"/> exists for outbound notifications
/// (Courier email / SMS / push) where the recipient's preferred locale is
/// known from their user profile and the rendered text must be inlined into
/// the notification payload before delivery.
/// </para>
/// <para>
/// All catalogs are loaded eagerly at construction; subsequent calls are
/// in-memory dictionary lookups (O(1)) plus a regex substitution for any
/// parameter bindings. <see cref="HasKey(string)"/> is O(1) via a pre-computed
/// <see cref="HashSet{T}"/> across all loaded catalogs.
/// </para>
/// </remarks>
public sealed partial class Translator : ITranslator
{
    private const string _SCHEMA_KEY = "$schema";

    private static readonly JsonSerializerOptions sr_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private static readonly Regex sr_paramPattern = ParamRegex();

    private readonly SupportedLocales r_supportedLocales;

    /// <summary>
    /// Locale code → (key → translated string). Outer key is the locale filename
    /// stem in canonical BCP 47 casing.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string>> r_catalogs = [];

    /// <summary>
    /// Pre-computed union of every key across every loaded catalog.
    /// O(1) <see cref="HasKey(string)"/>.
    /// </summary>
    private readonly HashSet<string> r_allKnownKeys = new(StringComparer.Ordinal);

    /// <summary>
    /// Initializes a new instance of the <see cref="Translator"/> class by
    /// loading all <c>*.json</c> message catalogs from the specified directory.
    /// </summary>
    /// <param name="supportedLocales">
    /// The configured <see cref="SupportedLocales"/> instance. Used to resolve
    /// requested locale codes to supported ones (canonical BCP 47, language
    /// fallback, base-locale fallback).
    /// </param>
    /// <param name="messagesDirectory">
    /// The absolute path to the directory containing locale JSON files
    /// (e.g. <c>{AppContext.BaseDirectory}/messages/</c>).
    /// </param>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="messagesDirectory"/> is null, empty, or whitespace.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the specified directory does not exist.
    /// </exception>
    public Translator(SupportedLocales supportedLocales, string messagesDirectory)
    {
        ArgumentNullException.ThrowIfNull(supportedLocales);
        messagesDirectory.ThrowIfFalsey();

        if (!Directory.Exists(messagesDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Messages directory not found: {messagesDirectory}");
        }

        r_supportedLocales = supportedLocales;
        LoadCatalogs(messagesDirectory);
    }

    /// <inheritdoc/>
    public string T(string locale, TKMessage message)
    {
        locale.ThrowIfFalsey();
        ArgumentNullException.ThrowIfNull(message);

        var resolvedLocale = r_supportedLocales.Resolve(locale);

        // Try requested locale first.
        if (r_catalogs.TryGetValue(resolvedLocale, out var catalog) &&
            catalog.TryGetValue(message.Key, out var value))
        {
            return Interpolate(value, message.Parameters);
        }

        // Fall back to base locale.
        if (resolvedLocale != r_supportedLocales.Base &&
            r_catalogs.TryGetValue(r_supportedLocales.Base, out var baseCatalog) &&
            baseCatalog.TryGetValue(message.Key, out var baseValue))
        {
            return Interpolate(baseValue, message.Parameters);
        }

        // No translation found anywhere — return the raw key (dev-readable).
        return message.Key;
    }

    /// <inheritdoc/>
    public bool HasKey(string key)
    {
        key.ThrowIfFalsey();
        return r_allKnownKeys.Contains(key);
    }

    /// <summary>
    /// Matches <c>{paramName}</c> placeholders in translation templates.
    /// <c>\w</c> can't match <c>}</c>, so the greedy <c>\w+</c> stops naturally
    /// at the closing brace without backtracking on the success path; failure
    /// case is at most O(n) once → no <c>matchTimeout</c> needed.
    /// </summary>
    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.None)]
    private static partial Regex ParamRegex();

    /// <summary>
    /// Replaces <c>{paramName}</c> placeholders in the template with values
    /// from the parameters dictionary. Unmatched placeholders are left as-is.
    /// </summary>
    private static string Interpolate(
        string template,
        IReadOnlyDictionary<string, string>? parameters)
    {
        if (parameters.Falsey())
        {
            return template;
        }

        return sr_paramPattern.Replace(template, match =>
        {
            var paramName = match.Groups[1].Value;
            return parameters!.TryGetValue(paramName, out var replacement)
                ? replacement
                : match.Value;
        });
    }

    /// <summary>
    /// Loads all <c>*.json</c> files from the specified directory into the
    /// catalog dictionary. Each file's name (without extension) is treated as
    /// the locale code. The <c>$schema</c> key is skipped.
    /// </summary>
    private void LoadCatalogs(string directory)
    {
        foreach (var filePath in Directory.EnumerateFiles(directory, "*.json"))
        {
            var locale = Path.GetFileNameWithoutExtension(filePath);
            var json = File.ReadAllText(filePath);

            var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(
                json,
                sr_jsonOptions);
            if (entries is null)
            {
                continue;
            }

            entries.Remove(_SCHEMA_KEY);
            var canonicalLocale = SupportedLocales.ToBcp47(locale);
            r_catalogs[canonicalLocale] = entries;

            foreach (var key in entries.Keys)
            {
                r_allKnownKeys.Add(key);
            }
        }
    }
}
