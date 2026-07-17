// -----------------------------------------------------------------------
// <copyright file="BaseFactoriesEmitter.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using DcsvIo.D2.SourceGen;

/// <summary>
/// Emits the generic cross-cutting catalog's CONSTRUCTING failure factories
/// (<see cref="FactoryHost.Base"/>): the semantic factories on the
/// <c>D2Result</c> / <c>D2Result&lt;TData&gt;</c> partial classes plus the
/// per-code boolean discriminators. Unlike the delegating
/// <see cref="FailuresEmitter"/> (per-domain catalogs), these factories ARE
/// the base — they call the <c>D2Result</c> constructor directly with the
/// status code + error code + default <c>userMessageKey</c> the spec declares.
/// Stateless and unit-testable in isolation; consumes the same valid-entry
/// walk (<see cref="ConstantsEmitter.Validate"/>) so the constants, factories,
/// and booleans files agree on the entry set.
/// </summary>
/// <remarks>
/// <para>
/// The body shape is fully determined by the spec fields. <c>factoryShape</c>
/// is the universal <c>standard</c> shape for every error factory:
/// <c>(messages?, inputErrors?, errorCode?, category?, traceId?)</c> — ALL
/// optional, so any factory can stamp a domain code + category and optionally
/// carry inputErrors. <c>httpStatus</c> maps to the BCL <c>HttpStatusCode</c>
/// member; <c>code</c> maps to the <c>ErrorCodes.&lt;Code&gt;</c> default;
/// <c>userMessageKey</c> is the default-message constant. Entries with shape
/// <c>none</c> emit a boolean (when the code keys one) but no factory — their
/// data-carrying or factory-less semantics stay hand-rolled / call-site-emitted.
/// </para>
/// <para>
/// The per-code booleans are emitted for every code whose shape is NOT
/// <c>none</c> PLUS the explicit boolean-keyed <c>none</c> codes
/// (<c>SOME_FOUND</c> / <c>PARTIAL_SUCCESS</c> / <c>IDEMPOTENCY_IN_FLIGHT</c>).
/// The serialization codes (<c>COULD_NOT_BE_*</c>) key no boolean. The
/// composite / status-based booleans (<c>IsOk</c> / <c>IsCreated</c> /
/// <c>IsPartialOrMissing</c> / <c>IsTransientRetryable</c>) are not code-keyed
/// and stay hand-rolled on the <c>D2Result</c> partial.
/// </para>
/// </remarks>
internal static class BaseFactoriesEmitter
{
    private const string _SHAPE_STANDARD = "standard";
    private const string _SHAPE_NONE = "none";

    // The none-shape codes that nonetheless key a per-code boolean (matching
    // the hand-rolled IsSomeFound / IsPartialSuccess / IsIdempotencyInFlight).
    // The serialization codes (COULD_NOT_BE_*) deliberately key no boolean.
    private static readonly ImmutableHashSet<string> sr_booleanKeyedNoneCodes =
        ImmutableHashSet.Create(
            System.StringComparer.Ordinal,
            "SOME_FOUND",
            "PARTIAL_SUCCESS",
            "IDEMPOTENCY_IN_FLIGHT");

    /// <summary>
    /// Emits the non-generic constructing factories file (factories ONTO the
    /// <c>partial class D2Result</c>).
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source (no diagnostics — validation runs upstream).</returns>
    public static EmitResult EmitFactories(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(
            spec, config, ImmutableHashSet<string>.Empty, discard);
        var source = EmitFactoriesSource(validEntries, config, generic: false);
        return new EmitResult(source, ImmutableArray<EmitDiagnostic>.Empty);
    }

    /// <summary>
    /// Emits the generic constructing factories file (the <c>&lt;TData&gt;</c>
    /// twins ONTO the <c>partial class D2Result&lt;TData&gt;</c>).
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source.</returns>
    public static EmitResult EmitGenericFactories(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(
            spec, config, ImmutableHashSet<string>.Empty, discard);
        var source = EmitFactoriesSource(validEntries, config, generic: true);
        return new EmitResult(source, ImmutableArray<EmitDiagnostic>.Empty);
    }

    /// <summary>
    /// Emits the per-code boolean discriminators file (booleans ONTO the
    /// <c>partial class D2Result</c>).
    /// </summary>
    /// <param name="spec">Parsed error-codes spec.</param>
    /// <param name="config">The catalog configuration.</param>
    /// <returns>Generated source.</returns>
    public static EmitResult EmitBooleans(ErrorCodesSpec spec, CatalogConfig config)
    {
        var discard = ImmutableArray.CreateBuilder<EmitDiagnostic>();
        var validEntries = ConstantsEmitter.Validate(
            spec, config, ImmutableHashSet<string>.Empty, discard);
        var source = EmitBooleansSource(validEntries, config);
        return new EmitResult(source, ImmutableArray<EmitDiagnostic>.Empty);
    }

    /// <summary>
    /// Whether an entry's <c>factoryShape</c> emits a constructing factory body
    /// in base mode. The universal <c>standard</c> shape emits a factory;
    /// <c>none</c> emits no factory.
    /// </summary>
    /// <param name="shape">The entry's <c>factoryShape</c>.</param>
    /// <returns><c>true</c> when a factory body is emitted.</returns>
    internal static bool EmitsFactory(string? shape) =>
        shape == _SHAPE_STANDARD;

    /// <summary>
    /// Whether a code keys a per-code boolean discriminator. Every non-<c>none</c>
    /// code does, plus the explicit boolean-keyed <c>none</c> codes. The
    /// serialization codes key no boolean (matching the hand-rolled surface).
    /// </summary>
    /// <param name="entry">The error-code entry.</param>
    /// <returns><c>true</c> when a boolean is emitted for the code.</returns>
    internal static bool EmitsBoolean(ErrorCodeEntry entry) =>
        entry.FactoryShape != _SHAPE_NONE
        || sr_booleanKeyedNoneCodes.Contains(entry.Code);

    /// <summary>
    /// Maps an HTTP status int to the BCL <see cref="System.Net.HttpStatusCode"/>
    /// member name used in the constructing factory body.
    /// </summary>
    /// <param name="httpStatus">The HTTP status from the spec entry.</param>
    /// <returns>The <c>HttpStatusCode</c> member name.</returns>
    internal static string StatusName(int httpStatus) => httpStatus switch
    {
        200 => "OK",
        206 => "PartialContent",
        207 => "MultiStatus",
        400 => "BadRequest",
        401 => "Unauthorized",
        403 => "Forbidden",
        404 => "NotFound",
        409 => "Conflict",
        413 => "RequestEntityTooLarge",
        429 => "TooManyRequests",
        500 => "InternalServerError",
        503 => "ServiceUnavailable",
        _ => "InternalServerError",
    };

    /// <summary>
    /// Maps a spec category wire string (snake_case) to its
    /// <c>ErrorCategory</c> PascalCase enum member name
    /// (<c>validation_failure</c> → <c>ValidationFailure</c>). The emitted
    /// factory body references <c>ErrorCategory.&lt;member&gt;</c>. Mirrors the
    /// registry emitter's identical mapping so the per-code category baked into
    /// the factory matches the registry's <c>ErrorCodeInfo.Category</c>.
    /// </summary>
    /// <param name="category">The snake_case category wire string.</param>
    /// <returns>The <c>ErrorCategory</c> enum member name.</returns>
    internal static string CategoryMemberName(string category)
    {
        var sb = new StringBuilder(category.Length);

        foreach (var segment in category.Split('_'))
        {
            if (segment.Length == 0)
                continue;

            sb.Append(char.ToUpperInvariant(segment[0]));

            if (segment.Length > 1)
                sb.Append(segment.Substring(1).ToLowerInvariant());
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a SCREAMING_SNAKE code to PascalCase for the boolean name
    /// (<c>NOT_FOUND</c> → <c>NotFound</c>, so the boolean is <c>IsNotFound</c>).
    /// </summary>
    /// <param name="code">The SCREAMING_SNAKE code.</param>
    /// <returns>The PascalCase form.</returns>
    internal static string PascalCase(string code)
    {
        var sb = new StringBuilder(code.Length);
        var upperNext = true;

        foreach (var c in code)
        {
            if (c == '_')
            {
                upperNext = true;
                continue;
            }

            sb.Append(upperNext ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
            upperNext = false;
        }

        return sb.ToString();
    }

    private static string EmitFactoriesSource(
        List<ErrorCodeEntry> entries, CatalogConfig config, bool generic)
    {
        var banner = generic ? config.BaseGenericFactoriesBanner! : config.BaseFactoriesBanner!;
        var summary = generic ? config.BaseGenericFactoriesSummary! : config.BaseFactoriesSummary!;
        var hostType = generic ? config.BaseGenericHostType! : config.BaseHostType!;
        var returnType = generic ? config.BaseGenericHostType! : config.BaseHostType!;

        var sb = new StringBuilder();
        EmitBlock(sb, banner);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Net;");
        sb.AppendLine("using DcsvIo.D2.ErrorCodes.Category;");
        sb.AppendLine("using DcsvIo.D2.I18n;");
        sb.AppendLine();
        sb.AppendLine($"namespace {config.RootNamespace};");
        sb.AppendLine();
        EmitBlock(sb, summary);

        // The non-generic host (D2Result) is the unsealed base; the generic host
        // (D2Result<TData>) is sealed and derives from it. The partial-class
        // modifier MUST match the primary declaration on both — a sealed mismatch
        // breaks the inheritance (CS0509).
        var sealedModifier = generic ? "sealed " : string.Empty;
        sb.AppendLine($"public {sealedModifier}partial class {hostType}");
        sb.AppendLine("{");

        var first = true;

        foreach (var entry in entries)
        {
            if (!EmitsFactory(entry.FactoryShape))
                continue;

            if (!first)
                sb.AppendLine();

            first = false;
            EmitFactory(sb, entry, config, generic, returnType);
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitFactory(
        StringBuilder sb,
        ErrorCodeEntry entry,
        CatalogConfig config,
        bool generic,
        string returnType)
    {
        var statusName = StatusName(entry.HttpStatus);
        var newKeyword = generic ? "new " : string.Empty;

        EmitFactoryDoc(sb, entry, generic);

        if (entry.Deprecated)
        {
            sb.AppendLine(
                $"    [System.Obsolete({ConstantsEmitter.ObsoleteMessageLiteral(entry)})]");
        }

        var categoryMember = CategoryMemberName(entry.Category!);

        // Signature — the one universal error-factory shape: every parameter is
        // optional so any factory can stamp a domain code + category and
        // optionally carry inputErrors.
        var sig = new StringBuilder();

        sig.Append("    public static ").Append(newKeyword).Append(returnType)
            .Append(' ').Append(entry.FactoryName).Append('(');

        sig.Append("IReadOnlyList<TKMessage>? messages = null");
        sig.Append(", IReadOnlyList<InputError>? inputErrors = null");
        sig.Append(", string? errorCode = null, ErrorCategory? category = null");
        sig.Append(", string? traceId = null)");
        sb.AppendLine(sig.ToString());
        sb.AppendLine("    {");
        sb.AppendLine($"        messages ??= [{entry.UserMessageKey}];");

        // Constructing body. Generic twins pass `default` data as the 2nd ctor
        // arg; inputErrors is passed positionally before statusCode. The
        // category is baked from the entry's declared category at generation
        // time; the errorCode / category overrides let a delegating domain
        // factory stamp its own code + category on this base status.
        var errorCodeArg = $"errorCode ?? {config.ConstantsClassName}.{entry.Code}";
        var categoryArg = $"category ?? ErrorCategory.{categoryMember}";

        sb.AppendLine("        return new(");
        sb.AppendLine("            false,");

        if (generic)
            sb.AppendLine("            default,");

        sb.AppendLine("            messages,");
        sb.AppendLine("            inputErrors,");
        sb.AppendLine($"            statusCode: HttpStatusCode.{statusName},");
        sb.AppendLine($"            errorCode: {errorCodeArg},");
        sb.AppendLine("            traceId: traceId,");
        sb.AppendLine($"            category: {categoryArg});");
        sb.AppendLine("    }");
    }

    private static void EmitFactoryDoc(
        StringBuilder sb, ErrorCodeEntry entry, bool generic)
    {
        sb.AppendLine($"    /// <summary>{EscapeXmlDoc(entry.Doc)}</summary>");
        sb.AppendLine(
            "    /// <param name=\"messages\">Optional translation messages; defaults to "
            + $"<c>[{EscapeXmlDoc(entry.UserMessageKey ?? string.Empty)}]</c>.</param>");

        sb.AppendLine("    /// <param name=\"inputErrors\">Optional per-field input errors.</param>");
        sb.AppendLine(
            "    /// <param name=\"errorCode\">Optional override for the default error "
            + "code so callers can attach a more specific code.</param>");

        sb.AppendLine(
            "    /// <param name=\"category\">Optional override for the default error "
            + "category so a delegating factory can stamp its own code's category.</param>");

        sb.AppendLine("    /// <param name=\"traceId\">Optional trace identifier.</param>");

        var returnsRef = generic
            ? "A pre-built typed <see cref=\"D2Result{TData}\"/> failure."
            : "A pre-built <see cref=\"D2Result\"/> failure.";

        sb.AppendLine($"    /// <returns>{returnsRef}</returns>");
    }

    private static string EmitBooleansSource(List<ErrorCodeEntry> entries, CatalogConfig config)
    {
        var sb = new StringBuilder();
        EmitBlock(sb, config.BaseBooleansBanner!);
        sb.AppendLine();
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Text.Json.Serialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {config.RootNamespace};");
        sb.AppendLine();
        EmitBlock(sb, config.BaseBooleansSummary!);

        // Booleans land on the non-generic, unsealed D2Result partial.
        sb.AppendLine($"public partial class {config.BaseHostType}");
        sb.AppendLine("{");

        var first = true;
        foreach (var entry in entries)
        {
            if (!EmitsBoolean(entry))
                continue;

            if (!first)
                sb.AppendLine();

            first = false;
            EmitBoolean(sb, entry, config);
        }

        sb.AppendLine("}");
        return sb.ToString().LfNormalized();
    }

    private static void EmitBoolean(StringBuilder sb, ErrorCodeEntry entry, CatalogConfig config)
    {
        var name = "Is" + PascalCase(entry.Code);
        sb.AppendLine("    /// <summary>");
        sb.AppendLine(
            $"    /// Gets a value indicating whether this result carries the "
            + $"<see cref=\"{config.ConstantsClassName}.{entry.Code}\"/> error code.");

        sb.AppendLine("    /// </summary>");

        if (entry.Deprecated)
        {
            sb.AppendLine(
                $"    [System.Obsolete({ConstantsEmitter.ObsoleteMessageLiteral(entry)})]");
        }

        sb.AppendLine("    [JsonIgnore]");

        sb.AppendLine(
            $"    public bool {name} => ErrorCode == {config.ConstantsClassName}.{entry.Code};");
    }

    /// <summary>
    /// Appends a newline-delimited config block one line at a time via
    /// <see cref="StringBuilder.AppendLine()"/>. Every emitted line is later
    /// collapsed to a bare LF at the source funnels
    /// (<see cref="EmitFactoriesSource"/> / <see cref="EmitBooleansSource"/>
    /// each return <c>LfNormalized()</c>), so the generated source is LF-only
    /// regardless of the build host's <c>Environment.NewLine</c>.
    /// </summary>
    private static void EmitBlock(StringBuilder sb, string block)
    {
        var lines = block.Split('\n');
        var count = lines.Length;

        if (count > 0 && lines[count - 1].Length == 0)
            count--;

        for (var i = 0; i < count; i++)
            sb.AppendLine(lines[i]);
    }

    private static string EscapeXmlDoc(string value) => value
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;");
}
