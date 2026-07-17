// -----------------------------------------------------------------------
// <copyright file="GeoCatalogEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Geo.SourceGen.Emitters;

using System;
using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using DcsvIo.D2.Geo.SourceGen.Spec;
using DcsvIo.D2.SourceGen;
using DcsvIo.D2.SourceGen.Polyfills;

/// <summary>
/// Emits the static <c>GeoCatalog</c> class with two constants surfacing
/// the catalog metadata: <c>CatalogVersion</c> (string) +
/// <c>CatalogPublishedAt</c> (<see cref="DateTimeOffset"/> — NOT NodaTime
/// <c>Instant</c>, so this assembly stays NodaTime-free).
/// Sourced from the pipeline-derived spec metadata (<c>generatedAt</c>);
/// falls back to <c>DateTimeOffset.MinValue</c> when no pipeline-spec
/// metadata is present (degenerate-catalog safety).
/// </summary>
internal static class GeoCatalogEmitter
{
    private const string _NAMESPACE = EmitterHelpers.AbstractionsNamespace;

    /// <summary>
    /// Emits the singleton <c>GeoCatalog</c> static class. Metadata is taken
    /// from <c>countries.spec.json</c> when present (it is the largest and
    /// always pipeline-derived); falls back to whichever pipeline spec is
    /// available, then to defaults.
    /// </summary>
    /// <param name="context">The aggregate spec context.</param>
    /// <returns>The single emit result.</returns>
    public static ImmutableArray<EmitResult> EmitAll(GeoSpecContext context)
    {
        var metadata = PickMetadata(context);
        var version = metadata?.CatalogVersion ?? "0.0.0";
        var publishedAt = ParsePublishedAt(metadata?.GeneratedAt);

        var sb = new StringBuilder();
        AppendFileHeader(sb);
        sb.AppendLine();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {_NAMESPACE};");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine(
            "/// Provenance constants for the geo catalog. Surfaced so consumers can");
        sb.AppendLine(
            "/// confirm the running version (e.g. observability tags, ETag headers,");
        sb.AppendLine(
            "/// audit metadata). <see cref=\"CatalogPublishedAt\"/> uses");
        sb.AppendLine(
            "/// <see cref=\"DateTimeOffset\"/> (NOT NodaTime <c>Instant</c>) — this");
        sb.AppendLine(
            "/// lib stays NodaTime-free so it can be a leaf dependency.");
        sb.AppendLine("/// </summary>");
        sb.AppendLine("public static class GeoCatalog");
        sb.AppendLine("{");
        sb.AppendLine(
            $"    /// <summary>Catalog version — surfaced from spec "
            + "metadata.</summary>");
        sb.AppendLine(
            $"    public const string CatalogVersion = \"{EscapeStringLiteral(version)}\";");
        sb.AppendLine();
        sb.AppendLine(
            $"    /// <summary>Catalog publication timestamp (UTC). Sourced from "
            + "spec metadata.</summary>");
        sb.AppendLine(
            "    public static readonly DateTimeOffset CatalogPublishedAt =");
        sb.AppendLine(
            $"        new DateTimeOffset({publishedAt.Year}, {publishedAt.Month}, "
            + $"{publishedAt.Day}, {publishedAt.Hour}, {publishedAt.Minute}, "
            + $"{publishedAt.Second}, {publishedAt.Millisecond}, TimeSpan.Zero);");
        sb.AppendLine("}");

        return ImmutableArray.Create(new EmitResult(
            HintName: "GeoCatalog.g.cs",
            GeneratedSource: sb.ToString().LfNormalized(),
            Diagnostics: ImmutableArray<EmitDiagnostic>.Empty));
    }

    private static SpecMetadata? PickMetadata(GeoSpecContext context)
    {
        return context.Countries?.Metadata
            ?? context.Subdivisions?.Metadata
            ?? context.Currencies?.Metadata
            ?? context.Languages?.Metadata
            ?? context.Locales?.Metadata
            ?? context.Timezones?.Metadata
            ?? context.GeopoliticalEntities?.Metadata;
    }

    private static DateTimeOffset ParsePublishedAt(string? generatedAt)
    {
        if (generatedAt.Falsey())
            return DateTimeOffset.MinValue.ToUniversalTime();

        if (DateTimeOffset.TryParse(
                generatedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            return parsed;
        }

        return DateTimeOffset.MinValue.ToUniversalTime();
    }

    private static void AppendFileHeader(StringBuilder sb) =>
        EmitterHelpers.AppendFileHeader(sb);

    private static string EscapeStringLiteral(string value) =>
        EmitterHelpers.EscapeStringLiteral(value);
}
