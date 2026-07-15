// -----------------------------------------------------------------------
// <copyright file="CatalogConfig.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.ErrorCodes.SourceGen;

/// <summary>
/// Per-catalog configuration carried by a thin <c>ErrorCodesGenerator</c>
/// shell into the shared <see cref="ErrorCodesEngine"/>. Every string that
/// affects the emitted bytes (class names, namespace, banner, doc-comment
/// nouns) is config-driven so a single engine reproduces every catalog's
/// output byte-for-byte. Adding a catalog is a config row — no new emitter
/// logic.
/// </summary>
/// <param name="TargetAssemblyName">
/// The consuming assembly the catalog emits into (the dispatch gate, e.g.
/// <c>DcsvIo.D2.Auth</c> / <c>DcsvIo.D2.Result</c>).
/// </param>
/// <param name="SpecFileName">
/// The spec filename surfaced via <c>AdditionalFiles</c> (e.g.
/// <c>auth-error-codes.spec.json</c>).
/// </param>
/// <param name="RootNamespace">
/// The namespace the emitted constants + failures classes live in (e.g.
/// <c>DcsvIo.D2.Auth.Errors</c> / <c>DcsvIo.D2.Result</c>).
/// </param>
/// <param name="ConstantsClassName">
/// The emitted constants class name (e.g. <c>AuthErrorCodes</c> /
/// <c>ErrorCodes</c>).
/// </param>
/// <param name="ConstantsSourceName">
/// The constants <c>.g.cs</c> hint name (e.g. <c>AuthErrorCodes.g.cs</c> /
/// <c>ErrorCodes.g.cs</c>).
/// </param>
/// <param name="ConstantsBanner">
/// The exact auto-generated header banner block emitted verbatim on the
/// constants file (carried per-catalog because the two current catalogs'
/// banners differ in line-split shape).
/// </param>
/// <param name="ConstantsSummary">
/// The exact XML <c>&lt;summary&gt;</c>/<c>&lt;remarks&gt;</c> doc block
/// rendered above the constants class (per-catalog wording).
/// </param>
/// <param name="AllCodesDoc">
/// The exact XML doc-comment block (each line including its <c>///</c>
/// prefix + indentation, newline-joined) rendered above the <c>AllCodes</c>
/// member. Carried verbatim per-catalog because the two catalogs' doc
/// comments differ in both wording and line-wrap shape.
/// </param>
/// <param name="GetHttpStatusDoc">
/// The exact XML doc-comment block rendered above the <c>GetHttpStatus</c>
/// member (verbatim per-catalog).
/// </param>
/// <param name="DomainPrefix">
/// The enforced SCREAMING_SNAKE code prefix (e.g. <c>AUTH_</c>); <c>null</c>
/// for the generic catalog (the reserved unprefixed namespace) which is
/// exempt from the domain-prefix diagnostic.
/// </param>
/// <param name="EmitKebabCase">
/// <c>true</c> when the constants class emits the <c>KebabCase</c> helper
/// (auth-only today).
/// </param>
/// <param name="EmitFailures">
/// <c>true</c> when the catalog emits a <c>&lt;Domain&gt;Failures</c> factory
/// file (requires the factory fields on every entry).
/// </param>
/// <param name="FailuresClassName">
/// The emitted failures class name (e.g. <c>AuthFailures</c>); ignored when
/// <see cref="EmitFailures"/> is <c>false</c>.
/// </param>
/// <param name="FailuresSourceName">
/// The failures <c>.g.cs</c> hint name (e.g. <c>AuthFailures.g.cs</c>);
/// ignored when <see cref="EmitFailures"/> is <c>false</c>.
/// </param>
/// <param name="FailuresBanner">
/// The exact auto-generated header banner block emitted verbatim on the
/// failures file; ignored when <see cref="EmitFailures"/> is <c>false</c>.
/// </param>
/// <param name="FailuresSummary">
/// The exact XML <c>&lt;summary&gt;</c>/<c>&lt;remarks&gt;</c> doc block
/// rendered above the failures class; ignored when <see cref="EmitFailures"/>
/// is <c>false</c>.
/// </param>
/// <param name="ValidateCategory">
/// <c>true</c> when the catalog validates <c>category</c> enum membership +
/// <c>factoryName</c> uniqueness (auth); <c>false</c> for the generic
/// constants-only catalog which validates the <c>code</c> regex + <c>doc</c>
/// presence instead.
/// </param>
/// <param name="MalformedSpecId">
/// The catalog's malformed-spec diagnostic id (<c>D2EC001</c> /
/// <c>D2AEC001</c>) — surfaced by the loader.
/// </param>
/// <param name="DuplicateCodeId">
/// The catalog's duplicate-code diagnostic id (<c>D2EC002</c> /
/// <c>D2AEC003</c>).
/// </param>
/// <param name="InvalidHttpStatusId">
/// The catalog's unsupported-httpStatus diagnostic id (<c>D2EC003</c> /
/// <c>D2AEC005</c>).
/// </param>
/// <param name="InvalidCodeId">
/// The generic catalog's malformed-code diagnostic id (<c>D2EC004</c>);
/// <c>null</c> when <see cref="ValidateCategory"/> is <c>true</c> (auth
/// relies on the schema's <c>^AUTH_…</c> regex).
/// </param>
/// <param name="MissingDocId">
/// The generic catalog's missing-doc diagnostic id (<c>D2EC005</c>);
/// <c>null</c> when <see cref="ValidateCategory"/> is <c>true</c>.
/// </param>
/// <param name="UnknownCategoryId">
/// The auth catalog's unknown-category diagnostic id (<c>D2AEC002</c>);
/// <c>null</c> when <see cref="ValidateCategory"/> is <c>false</c>.
/// </param>
/// <param name="DuplicateFactoryNameId">
/// The auth catalog's duplicate-factoryName diagnostic id (<c>D2AEC004</c>);
/// <c>null</c> when <see cref="ValidateCategory"/> is <c>false</c>.
/// </param>
/// <param name="FactoryHost">
/// Selects how the failure factories are emitted (orthogonal to the per-entry
/// <c>factoryShape</c>). <see cref="SourceGen.FactoryHost.Domain"/> (default,
/// auth): delegating <c>&lt;Domain&gt;Failures</c> + <c>&lt;Domain&gt;Failures&lt;T&gt;</c>.
/// <see cref="SourceGen.FactoryHost.Base"/> (generic): constructing factories
/// ONTO the <c>D2Result</c> / <c>D2Result&lt;TData&gt;</c> partials + per-code
/// booleans. Ignored when <see cref="EmitFailures"/> is <c>false</c>.
/// </param>
/// <param name="GenericFailuresSourceName">
/// The generic delegating failures <c>.g.cs</c> hint name (e.g.
/// <c>AuthFailures.Generic.g.cs</c>) for the <c>&lt;Domain&gt;Failures&lt;T&gt;</c>
/// class. Required when <see cref="FactoryHost"/> is
/// <see cref="SourceGen.FactoryHost.Domain"/> and <see cref="EmitFailures"/>
/// is <c>true</c>; ignored otherwise.
/// </param>
/// <param name="GenericFailuresSummary">
/// The exact XML <c>&lt;summary&gt;</c>/<c>&lt;remarks&gt;</c> doc block rendered
/// above the <c>&lt;Domain&gt;Failures&lt;T&gt;</c> class; required for
/// <see cref="SourceGen.FactoryHost.Domain"/>, ignored otherwise.
/// </param>
/// <param name="BaseHostType">
/// The non-generic host type the base-mode constructing factories + booleans
/// land on (e.g. <c>D2Result</c>). Required when <see cref="FactoryHost"/> is
/// <see cref="SourceGen.FactoryHost.Base"/>; ignored otherwise.
/// </param>
/// <param name="BaseGenericHostType">
/// The generic host type the base-mode <c>&lt;TData&gt;</c> constructing
/// factories land on (e.g. <c>D2Result&lt;TData&gt;</c>). Required for
/// <see cref="SourceGen.FactoryHost.Base"/>, ignored otherwise.
/// </param>
/// <param name="BaseFactoriesSourceName">
/// The non-generic base-mode factories <c>.g.cs</c> hint name (e.g.
/// <c>D2Result.Factories.g.cs</c>). Required for
/// <see cref="SourceGen.FactoryHost.Base"/>, ignored otherwise.
/// </param>
/// <param name="BaseFactoriesBanner">
/// The auto-generated header banner emitted verbatim on the non-generic
/// base-mode factories file. Required for <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseFactoriesSummary">
/// The XML doc block rendered above the non-generic base-mode factories partial
/// class. Required for <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseGenericFactoriesSourceName">
/// The generic base-mode factories <c>.g.cs</c> hint name (e.g.
/// <c>D2Result.Generic.Factories.g.cs</c>). Required for
/// <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseGenericFactoriesBanner">
/// The banner emitted on the generic base-mode factories file. Required for
/// <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseGenericFactoriesSummary">
/// The XML doc block above the generic base-mode factories partial class.
/// Required for <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseBooleansSourceName">
/// The per-code booleans <c>.g.cs</c> hint name (e.g.
/// <c>D2Result.Booleans.g.cs</c>). Required for
/// <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseBooleansBanner">
/// The banner emitted on the booleans file. Required for
/// <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="BaseBooleansSummary">
/// The XML doc block above the booleans partial class. Required for
/// <see cref="SourceGen.FactoryHost.Base"/>.
/// </param>
/// <param name="MessageKeyClassName">
/// C# class name used when emitting default <c>userMessageKey</c> expressions
/// (public catalogs: <c>TK</c>; private product union: <c>ProductTK</c>).
/// Spec <c>userMessageKey</c> values remain <c>TK.*</c> for inverse-resolve;
/// emission maps the root identifier to this class name.
/// </param>
/// <param name="MessageKeyUsingNamespace">
/// Namespace imported for the message-key class (public:
/// <c>DcsvIo.D2.I18n</c>; private: <c>DcsvIo.D2.Private.I18n</c>).
/// </param>
internal sealed record CatalogConfig(
    string TargetAssemblyName,
    string SpecFileName,
    string RootNamespace,
    string ConstantsClassName,
    string ConstantsSourceName,
    string ConstantsBanner,
    string ConstantsSummary,
    string AllCodesDoc,
    string GetHttpStatusDoc,
    string? DomainPrefix,
    bool EmitKebabCase,
    bool EmitFailures,
    string? FailuresClassName,
    string? FailuresSourceName,
    string? FailuresBanner,
    string? FailuresSummary,
    bool ValidateCategory,
    string MalformedSpecId,
    string DuplicateCodeId,
    string InvalidHttpStatusId,
    string? InvalidCodeId,
    string? MissingDocId,
    string? UnknownCategoryId,
    string? DuplicateFactoryNameId,
    FactoryHost FactoryHost = FactoryHost.Domain,
    string? GenericFailuresSourceName = null,
    string? GenericFailuresSummary = null,
    string? BaseHostType = null,
    string? BaseGenericHostType = null,
    string? BaseFactoriesSourceName = null,
    string? BaseFactoriesBanner = null,
    string? BaseFactoriesSummary = null,
    string? BaseGenericFactoriesSourceName = null,
    string? BaseGenericFactoriesBanner = null,
    string? BaseGenericFactoriesSummary = null,
    string? BaseBooleansSourceName = null,
    string? BaseBooleansBanner = null,
    string? BaseBooleansSummary = null,
    string MessageKeyClassName = "TK",
    string MessageKeyUsingNamespace = "DcsvIo.D2.I18n");
