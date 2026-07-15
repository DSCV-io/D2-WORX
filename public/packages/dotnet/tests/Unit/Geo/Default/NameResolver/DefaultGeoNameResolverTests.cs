// -----------------------------------------------------------------------
// <copyright file="DefaultGeoNameResolverTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Geo.Default.NameResolver;

using System.Net;
using AwesomeAssertions;
using DcsvIo.D2.Geo.Abstractions;
using DcsvIo.D2.Geo.Default;
using DcsvIo.D2.Geo.Default.NameResolution;
using DcsvIo.D2.I18n;
using Xunit;

/// <summary>
/// Coverage for <see cref="DefaultGeoNameResolver"/> — cascade behavior,
/// boundary contracts, DoS guard, ambiguity sentinel, parent-context
/// discipline. Tests on a singleton resolver instance because the
/// resolver is stateless.
/// </summary>
public sealed class DefaultGeoNameResolverTests
{
    private readonly DefaultGeoNameResolver _resolver = new();

    // §1.2 category: Input validation
    [Fact]
    public void TryResolveCountryByName_NullInput_ReturnsValidationFailed()
    {
        var result = _resolver.TryResolveCountryByName(null!);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void TryResolveCountryByName_EmptyInput_ReturnsValidationFailed()
    {
        var result = _resolver.TryResolveCountryByName(string.Empty);
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void TryResolveCountryByName_WhitespaceInput_ReturnsValidationFailed()
    {
        var result = _resolver.TryResolveCountryByName("   ");
        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    // §1.2 category: Resource exhaustion / Security-adversarial
    [Fact]
    public void TryResolveCountryByName_OversizedInput_ReturnsValidationFailedTooLong()
    {
        var oversized = new string('a', DefaultGeoNameResolver.MAX_NAME_LENGTH + 1);

        var result = _resolver.TryResolveCountryByName(oversized);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.TOO_LONG.Key);
    }

    [Fact]
    public void TryResolveCountryByName_OversizedInput_OneMB_RejectsInUnderTenMs()
    {
        // The length guard is an O(1) int comparison — any realistic machine
        // rejects in microseconds. 500 ms gives 10 000× headroom against the
        // microsecond actual cost, so this only catches a true regression
        // (e.g., the guard accidentally allocates or copies the 1 MB string)
        // without flaking under heavy CPU load.
        var oversized = new string('x', 1_048_576);
        var start = System.Diagnostics.Stopwatch.GetTimestamp();

        var result = _resolver.TryResolveCountryByName(oversized);

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        result.Success.Should().BeFalse();
        elapsed.TotalMilliseconds.Should().BeLessThan(500);
    }

    [Fact]
    public void TryResolveCountryByName_InputAtMaxLength_PassesPredicate0()
    {
        // At-cap (256) input is allowed; we don't expect a match for arbitrary
        // 256-char string — just verify the cap is INCLUSIVE.
        var atCap = new string('a', DefaultGeoNameResolver.MAX_NAME_LENGTH);

        var result = _resolver.TryResolveCountryByName(atCap);

        // Outcome is NotFound (cascade exhausted), not TOO_LONG.
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — Pass-1 exact match.
    [Fact]
    public void TryResolveCountryByName_ExactDisplayName_ReturnsCountry()
    {
        var result = _resolver.TryResolveCountryByName("United States");

        result.Success.Should().BeTrue();
        result.Data.Should().NotBeNull();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void TryResolveCountryByName_ExactOfficialName_ReturnsCountry()
    {
        var result = _resolver.TryResolveCountryByName("United States of America");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void TryResolveCountryByName_ExactAlpha3Code_ReturnsCountry()
    {
        var result = _resolver.TryResolveCountryByName("USA");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void TryResolveCountryByName_CaseInsensitive_ReturnsCountry()
    {
        var result = _resolver.TryResolveCountryByName("UNITED STATES");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void TryResolveCountryByName_WhitespaceCollapse_ReturnsCountry()
    {
        var result = _resolver.TryResolveCountryByName("United  States");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    // §1.2 category: Domain-specific — confusable pairs.
    [Theory]
    [InlineData("Niger", "NE")]
    [InlineData("Nigeria", "NG")]
    [InlineData("Iran", "IR")]
    [InlineData("Iraq", "IQ")]
    [InlineData("Slovakia", "SK")]
    [InlineData("Slovenia", "SI")]
    [InlineData("Austria", "AT")]
    [InlineData("Australia", "AU")]
    [InlineData("Chad", "TD")]
    [InlineData("Chile", "CL")]
    public void TryResolveCountryByName_ConfusablePair_ReturnsDistinctCountry(
        string input, string expectedAlpha2)
    {
        var result = _resolver.TryResolveCountryByName(input);

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.ToString().Should().Be(expectedAlpha2);
    }

    // §1.2 category: Domain-specific — cascade exhausted.
    [Fact]
    public void TryResolveCountryByName_NoMatch_ReturnsNotFound()
    {
        var result = _resolver.TryResolveCountryByName(
            "SomeRandomCountryNameThatDoesNotExist");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TryResolveCountryByName_OneChar_TooShortForAnyPass_ReturnsNotFound()
    {
        var result = _resolver.TryResolveCountryByName("X");
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — subdivision parent-context.
    [Fact]
    public void TryResolveSubdivisionByName_GeorgiaInUS_ReturnsUSGA()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("Georgia", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-GA");
    }

    [Fact]
    public void TryResolveSubdivisionByName_GeorgiaInCA_ReturnsNotFound()
    {
        var ca = CountryLookup.ByCode[CountryCode.CA];

        var result = _resolver.TryResolveSubdivisionByName("Georgia", ca);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TryResolveSubdivisionByName_CaliforniaInUS_ReturnsUSCA()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("California", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-CA");
    }

    [Fact]
    public void TryResolveSubdivisionByName_NullParent_ReturnsValidationFailed()
    {
        var result = _resolver.TryResolveSubdivisionByName("California", null!);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.NOT_NULL_VIOLATION.Key);
    }

    [Fact]
    public void TryResolveSubdivisionByName_EmptyName_ReturnsValidationFailed()
    {
        var us = CountryLookup.ByCode[CountryCode.US];
        var result = _resolver.TryResolveSubdivisionByName(string.Empty, us);
        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void TryResolveSubdivisionByName_CountryWithNoSubdivisions_AQ_ReturnsNotFound()
    {
        var aq = CountryLookup.ByCode[CountryCode.AQ];

        var result = _resolver.TryResolveSubdivisionByName("AnythingAtAll", aq);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Error propagation — traceId discipline.
    [Fact]
    public void TryResolveCountryByName_ResultHasNullTraceId()
    {
        // Resolver is request-context-free — TraceId is null; callers
        // replay handler-scoped traceId via WithTraceId at the call site.
        var result = _resolver.TryResolveCountryByName("United States");

        result.TraceId.Should().BeNull();
    }

    // §1.2 category: Domain-specific — Pass-1 exact via additional name fields.
    [Fact]
    public void TryResolveCountryByName_ExactNumericCode_840_ReturnsUS()
    {
        var result = _resolver.TryResolveCountryByName("840");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.US);
    }

    [Fact]
    public void TryResolveCountryByName_ExactOfficialName_IvoryCoast_ReturnsCI()
    {
        var result = _resolver.TryResolveCountryByName("Ivory Coast");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.CI);
    }

    [Fact]
    public void TryResolveCountryByName_AmpersandToken_TrinidadAndTobago_ReturnsTT()
    {
        // The ampersand-token normalizer rewrites " & " to " and ", so
        // both DisplayName "Trinidad & Tobago" and OfficialName
        // "Trinidad and Tobago" resolve to TT regardless of the input form.
        _resolver.TryResolveCountryByName("Trinidad & Tobago").Data!
            .Iso31661Alpha2Code.Should().Be(CountryCode.TT);
        _resolver.TryResolveCountryByName("Trinidad and Tobago").Data!
            .Iso31661Alpha2Code.Should().Be(CountryCode.TT);
    }

    [Fact]
    public void TryResolveCountryByName_ExactOfficialName_HolySee_ReturnsVA()
    {
        var result = _resolver.TryResolveCountryByName("Holy See");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.VA);
    }

    [Fact]
    public void TryResolveCountryByName_ExactEndonym_CjkJapanese_ReturnsJP()
    {
        var result = _resolver.TryResolveCountryByName("日本");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.JP);
    }

    [Fact]
    public void TryResolveCountryByName_ExactEndonym_CjkChinese_ReturnsCN()
    {
        var result = _resolver.TryResolveCountryByName("中华人民共和国");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.CN);
    }

    [Fact]
    public void TryResolveCountryByName_ExactEndonym_RtlArabic_ReturnsSA()
    {
        var result = _resolver.TryResolveCountryByName("السعودية");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.SA);
    }

    [Fact]
    public void TryResolveCountryByName_ExactEndonym_GermanDeutschland_ReturnsDE()
    {
        var result = _resolver.TryResolveCountryByName("Deutschland");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.DE);
    }

    // §1.2 category: Domain-specific — NFD normalization.
    [Fact]
    public void TryResolveCountryByName_NfdNormalize_TurkiyeWithoutDiacritic_ReturnsTR()
    {
        // Catalog DisplayName is "Türkiye". After NFD-strip the ü becomes u
        // so input "Turkiye" (no diacritic) resolves identically.
        var result = _resolver.TryResolveCountryByName("Turkiye");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.TR);
    }

    [Fact]
    public void TryResolveCountryByName_NfdNormalize_TurkishDottedI_ReturnsTR()
    {
        // Locale-edge: TÜRKİYE uses the Turkish dotted İ. The normalizer
        // uses invariant casefold (NOT tr-TR) so dotted İ becomes 'i'
        // (NOT the Turkish dotless 'ı' that tr-TR.ToLower would emit).
        // Combined with NFD-strip on Ü, the input collapses to "turkiye"
        // and matches the catalog.
        var result = _resolver.TryResolveCountryByName("TÜRKİYE");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.TR);
    }

    // §1.2 category: Domain-specific — Pass-2 startsWith.
    [Fact]
    public void TryResolveCountryByName_Pass2Ambiguity_Unit_ReturnsNotFound()
    {
        // startsWith "unit" matches United States / United Kingdom /
        // United Arab Emirates — multiple distinct records → fail-closed
        // NotFound, never a guess.
        var result = _resolver.TryResolveCountryByName("Unit");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TryResolveCountryByName_Pass2Ambiguity_United_ReturnsNotFound()
    {
        // startsWith "united" matches multiple distinct records → NotFound.
        var result = _resolver.TryResolveCountryByName("United");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — Pass-3 contains.
    [Fact]
    public void TryResolveCountryByName_Pass3Unique_Burma_ReturnsMM()
    {
        // 'burma' is a substring of DisplayName "Myanmar (Burma)" only.
        var result = _resolver.TryResolveCountryByName("Burma");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.MM);
    }

    [Fact]
    public void TryResolveCountryByName_Pass3Unique_Vatican_ReturnsVA()
    {
        // 'vatican' is a substring of DisplayName "Vatican City" + endonym
        // "Civitas Vaticana" — both pointing at the same VA record, which
        // the cache-build de-duplicates rather than flagging ambiguous.
        var result = _resolver.TryResolveCountryByName("Vatican");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.VA);
    }

    [Fact]
    public void TryResolveCountryByName_Pass3Unique_Macedonia_ReturnsMK()
    {
        // 'macedonia' is a substring of DisplayName "North Macedonia" only.
        var result = _resolver.TryResolveCountryByName("Macedonia");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.MK);
    }

    [Fact]
    public void TryResolveCountryByName_Pass3Ambiguity_Korea_ReturnsNotFound()
    {
        // 'korea' substring matches both KP and KR → fail-closed NotFound.
        var result = _resolver.TryResolveCountryByName("Korea");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TryResolveCountryByName_Pass3Ambiguity_Republic_ReturnsNotFound()
    {
        // 'republic' appears in dozens of official-name fields →
        // fail-closed NotFound (proves Pass-3 enforces ambiguity discipline
        // even when many candidates exist).
        var result = _resolver.TryResolveCountryByName("Republic");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — Pass-3 short-circuit by exact match.
    [Fact]
    public void TryResolveCountryByName_ExactCongo_ShortCircuitsBeforePass3()
    {
        // 'Congo' is OfficialName of CG (Republic of the Congo). Pass-1
        // exact match wins before Pass-3 contains can surface ambiguity
        // with CD ("Congo - Kinshasa") + CG.
        var result = _resolver.TryResolveCountryByName("Congo");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.CG);
    }

    // §1.2 category: Domain-specific — Pass-4 Levenshtein fuzzy.
    [Fact]
    public void TryResolveCountryByName_Pass4Fuzzy_Astralia_ReturnsAU()
    {
        // Levenshtein("astralia", "australia") = 1 (one insertion); len=8
        // gives maxDistance = min(2, floor(8/5)) = 1 → within bound.
        var result = _resolver.TryResolveCountryByName("Astralia");

        result.Success.Should().BeTrue();
        result.Data!.Iso31661Alpha2Code.Should().Be(CountryCode.AU);
    }

    [Fact]
    public void TryResolveCountryByName_Pass4Skipped_FourCharIram_ReturnsNotFound()
    {
        // 'Iram' (4 chars) — Pass-3 + Pass-4 require >= 5 chars (Predicate
        // 3/4 cap). Pass-1 exact misses; Pass-2 startsWith 'iram' misses.
        // Pinned behavior: 4-char fuzzy is NEVER rescued by Pass-4.
        var result = _resolver.TryResolveCountryByName("Iram");

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Resource exhaustion — Pass-4 banded bounding under load.
    [Fact]
    public void TryResolveCountryByName_Pass4_OversizedAtCap_CompletesWithinTimeout()
    {
        // 256-char input forces Pass-4 to run Levenshtein against every
        // catalog key; the banded-bounding distance cap (maxDistance = 2)
        // short-circuits the DP per-key so total wall-clock stays bounded.
        var atCap = new string('q', DefaultGeoNameResolver.MAX_NAME_LENGTH);
        var start = System.Diagnostics.Stopwatch.GetTimestamp();

        var result = _resolver.TryResolveCountryByName(atCap);

        var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        result.Success.Should().BeFalse();

        // Generous bound — covers cold-cache first call + full Pass-4 sweep.
        elapsed.TotalMilliseconds.Should().BeLessThan(500);
    }

    // §1.2 category: Domain-specific — subdivision Pass-3 ambiguity.
    [Fact]
    public void TryResolveSubdivisionByName_Pass3Ambiguity_Carolina_US_ReturnsNotFound()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("Carolina", us);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public void TryResolveSubdivisionByName_ExactBeatsAmbiguity_NorthCarolina_US_ReturnsNC()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("North Carolina", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-NC");
    }

    [Fact]
    public void TryResolveSubdivisionByName_ExactBeatsAmbiguity_SouthCarolina_US_ReturnsSC()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("South Carolina", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-SC");
    }

    [Fact]
    public void TryResolveSubdivisionByName_ParentContext_Washington_US_ReturnsWA()
    {
        // 'Washington' is the DisplayName of US-WA only; District of Columbia
        // (US-DC) carries no 'Washington' substring in any name field, so
        // Pass-1 exact resolves uniquely to WA — no ambiguity.
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("Washington", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-WA");
    }

    [Fact]
    public void TryResolveSubdivisionByName_OttawaInUS_ReturnsNotFound()
    {
        // Ottawa is a Canadian city, not a US subdivision; parent-context
        // discipline blocks cross-country leakage.
        var us = CountryLookup.ByCode[CountryCode.US];

        var result = _resolver.TryResolveSubdivisionByName("Ottawa", us);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — Pass-4 subdivision fuzzy.
    [Fact]
    public void TryResolveSubdivisionByName_Pass4Fuzzy_Califrnia_US_ReturnsUSCA()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        // Levenshtein("califrnia", "california") = 1; len=9 → maxDistance=1.
        var result = _resolver.TryResolveSubdivisionByName("Califrnia", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-CA");
    }

    [Fact]
    public void TryResolveSubdivisionByName_Pass4Fuzzy_Texxas_US_ReturnsUSTX()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        // Levenshtein("texxas", "texas") = 1; len=6 → maxDistance=1.
        var result = _resolver.TryResolveSubdivisionByName("Texxas", us);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("US-TX");
    }

    [Fact]
    public void TryResolveSubdivisionByName_Pass4Skipped_FourCharTexs_US_ReturnsNotFound()
    {
        var us = CountryLookup.ByCode[CountryCode.US];

        // 'Texs' (4 chars): Pass-3 + Pass-4 require >= 5 chars; Pass-2
        // startsWith 'texs' has no match in US subdivisions (Texas
        // startsWith 'texa', not 'texs'). Confirms 4-char inputs are NEVER
        // rescued by Pass-4 fuzzy.
        var result = _resolver.TryResolveSubdivisionByName("Texs", us);

        result.Success.Should().BeFalse();
        result.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // §1.2 category: Domain-specific — subdivision NFD normalization.
    [Fact]
    public void TryResolveSubdivisionByName_NfdNormalize_SaoPaulo_BR_ReturnsBRSP()
    {
        var br = CountryLookup.ByCode[CountryCode.BR];

        // Catalog DisplayName is "São Paulo"; input without diacritic
        // resolves via NFD-strip.
        var result = _resolver.TryResolveSubdivisionByName("Sao Paulo", br);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("BR-SP");
    }

    [Fact]
    public void TryResolveSubdivisionByName_NfdNormalize_IleDeFrance_FR_ReturnsFRIDF()
    {
        var fr = CountryLookup.ByCode[CountryCode.FR];

        // Catalog DisplayName is "Île-de-France"; input without diacritic
        // resolves via NFD-strip on the leading Î.
        var result = _resolver.TryResolveSubdivisionByName("Ile-de-France", fr);

        result.Success.Should().BeTrue();
        result.Data!.Iso31662Code.Value.Should().Be("FR-IDF");
    }

    // §1.2 category: Resource exhaustion — subdivision oversized input.
    [Fact]
    public void TryResolveSubdivisionByName_OversizedInput_ReturnsValidationFailedTooLong()
    {
        var us = CountryLookup.ByCode[CountryCode.US];
        var oversized = new string('a', DefaultGeoNameResolver.MAX_NAME_LENGTH + 1);

        var result = _resolver.TryResolveSubdivisionByName(oversized, us);

        result.Success.Should().BeFalse();
        result.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Common.Errors.TOO_LONG.Key);
    }

    // §1.2 category: Domain-specific — TK key wiring on every NotFound branch.
    [Fact]
    public void TryResolveCountryByName_Pass3Ambiguity_CarriesAmbiguousTkKey()
    {
        // The ambiguity branch (any pass) emits NAME_RESOLUTION_AMBIGUOUS;
        // cascade-exhausted branch emits NAME_RESOLUTION_NOT_FOUND. Pinning
        // the message keys protects against silent TK-key drift on either
        // branch.
        var ambiguous = _resolver.TryResolveCountryByName("Korea");
        ambiguous.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Errors.NAME_RESOLUTION_AMBIGUOUS.Key);
    }

    [Fact]
    public void TryResolveCountryByName_CascadeExhausted_CarriesNotFoundTkKey()
    {
        var noMatch = _resolver.TryResolveCountryByName(
            "ZzzzNoSuchCountryNameAnywhereZzzz");
        noMatch.Messages.Should().ContainSingle()
            .Which.Key.Should().Be(TK.Geo.Errors.NAME_RESOLUTION_NOT_FOUND.Key);
    }
}
