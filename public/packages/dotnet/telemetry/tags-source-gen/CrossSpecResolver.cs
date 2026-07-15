// -----------------------------------------------------------------------
// <copyright file="CrossSpecResolver.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Telemetry.Tags.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Resolves <c>valuesFromSpec</c> tag references against sibling spec files
/// surfaced via <c>AdditionalFiles</c>. Currently only the
/// <c>"auth-error-codes"</c> reference is supported (resolves to the
/// <c>code</c> field of every entry in the AuthErrorCodes spec).
/// </summary>
internal static class CrossSpecResolver
{
    /// <summary>The supported <c>valuesFromSpec</c> value for the AuthErrorCodes spec.</summary>
    public const string AUTH_ERROR_CODES_SPEC = "auth-error-codes";

    /// <summary>The expected file name of the AuthErrorCodes spec.</summary>
    public const string AUTH_ERROR_CODES_SPEC_FILE_NAME = "auth-error-codes.spec.json";

    /// <summary>
    /// Resolves the <paramref name="specName"/> reference against the
    /// available <paramref name="siblingSpecs"/>. Returns the resolved value
    /// list, or a <see cref="EmitDiagnostic"/> describing why the reference
    /// could not be resolved.
    /// </summary>
    /// <param name="specName">
    /// The cross-spec reference name (e.g. <c>"auth-error-codes"</c>).
    /// </param>
    /// <param name="siblingSpecs">
    /// All sibling spec files (path + content) surfaced via
    /// <c>AdditionalFiles</c>. The resolver matches by file name within this
    /// set.
    /// </param>
    /// <param name="instrument">Instrument name for diagnostic context.</param>
    /// <param name="tag">Tag name for diagnostic context.</param>
    /// <param name="meter">Meter name for diagnostic context.</param>
    /// <returns>
    /// A tuple of <c>(values, diagnostic)</c>. Exactly one is non-null /
    /// non-empty.
    /// </returns>
    public static (ImmutableArray<string> Values, EmitDiagnostic? Diagnostic) Resolve(
        string specName,
        ImmutableArray<SpecFile> siblingSpecs,
        string instrument,
        string tag,
        string meter)
    {
        if (specName == AUTH_ERROR_CODES_SPEC)
            return ResolveAuthErrorCodes(siblingSpecs, instrument, tag, meter);

        return (ImmutableArray<string>.Empty,
            EmitDiagnostics.CrossSpecInconsistency(
                instrument,
                tag,
                meter,
                specName,
                $"unknown valuesFromSpec reference '{specName}'; "
                + $"only '{AUTH_ERROR_CODES_SPEC}' is supported"));
    }

    private static (ImmutableArray<string> Values, EmitDiagnostic? Diagnostic)
        ResolveAuthErrorCodes(
            ImmutableArray<SpecFile> siblingSpecs,
            string instrument,
            string tag,
            string meter)
    {
        SpecFile? match = null;
        foreach (var sibling in siblingSpecs)
        {
            var name = System.IO.Path.GetFileName(sibling.Path);
            if (string.Equals(
                name,
                AUTH_ERROR_CODES_SPEC_FILE_NAME,
                System.StringComparison.OrdinalIgnoreCase))
            {
                match = sibling;
                break;
            }
        }

        if (match is null)
        {
            return (ImmutableArray<string>.Empty,
                EmitDiagnostics.CrossSpecInconsistency(
                    instrument,
                    tag,
                    meter,
                    AUTH_ERROR_CODES_SPEC,
                    $"sibling spec '{AUTH_ERROR_CODES_SPEC_FILE_NAME}' not found in "
                    + "AdditionalFiles; add it to the consuming csproj's <AdditionalFiles> set"));
        }

        try
        {
            using var doc = JsonDocument.Parse(match.Content);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("errorCodes", out var arr) ||
                arr.ValueKind != JsonValueKind.Array)
            {
                return (ImmutableArray<string>.Empty,
                    EmitDiagnostics.CrossSpecInconsistency(
                        instrument,
                        tag,
                        meter,
                        AUTH_ERROR_CODES_SPEC,
                        "spec root is not an object containing an 'errorCodes' array"));
            }

            var values = ImmutableArray.CreateBuilder<string>();
            var seen = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var element in arr.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object ||
                    !element.TryGetProperty("code", out var codeEl) ||
                    codeEl.ValueKind != JsonValueKind.String)
                    continue;

                var code = codeEl.GetString()!;
                if (seen.Add(code))
                    values.Add(code);
            }

            return (values.ToImmutable(), null);
        }
        catch (JsonException ex)
        {
            return (ImmutableArray<string>.Empty,
                EmitDiagnostics.CrossSpecInconsistency(
                    instrument,
                    tag,
                    meter,
                    AUTH_ERROR_CODES_SPEC,
                    $"failed to parse sibling spec: {ex.Message}"));
        }
    }
}
