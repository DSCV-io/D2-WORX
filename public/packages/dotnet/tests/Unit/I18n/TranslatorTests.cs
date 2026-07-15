// -----------------------------------------------------------------------
// <copyright file="TranslatorTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.I18n;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AwesomeAssertions;
using DcsvIo.D2.I18n;
using JetBrains.Annotations;
using Microsoft.Extensions.Configuration;
using Xunit;

/// <summary>
/// Unit tests for <see cref="Translator"/>. Builds a per-test temp catalog
/// directory (no shared state between tests, no reliance on the production
/// contracts/messages output).
/// </summary>
public sealed class TranslatorTests
{
    // ----------------------------------------------------------------------
    // Construction validation
    // ----------------------------------------------------------------------

    [Fact]
    public void Ctor_NullSupportedLocales_Throws()
    {
        using var dir = new TempCatalog();

        // ReSharper disable once AccessToDisposedClosure -- act is invoked
        // synchronously inside Should().Throw(), well before dir disposes.
        var act = () => new Translator(null!, dir.Path);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullMessagesDirectory_Throws()
    {
        var act = () => new Translator(NewSupportedLocales(), null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Ctor_EmptyOrWhitespaceMessagesDirectory_Throws(string path)
    {
        var act = () => new Translator(NewSupportedLocales(), path);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_NonExistentDirectory_ThrowsDirectoryNotFound()
    {
        var act = () => new Translator(
            NewSupportedLocales(),
            Path.Combine(Path.GetTempPath(), "definitely-does-not-exist-" + Guid.NewGuid()));
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Fact]
    public void Ctor_EmptyMessagesDirectory_Succeeds()
    {
        // Adversarial: empty dir is valid (no catalogs loaded) — every T() call
        // falls through to raw-key passthrough. Useful for tests that don't care
        // about translation content.
        using var dir = new TempCatalog();

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        translator.HasKey("any_key").Should().BeFalse();
    }

    [Fact]
    public void Ctor_MalformedJsonInDir_Throws()
    {
        using var dir = new TempCatalog();
        File.WriteAllText(Path.Combine(dir.Path, "en-US.json"), "{not valid json");

        // ReSharper disable once AccessToDisposedClosure -- act is invoked
        // synchronously inside Should().Throw(), well before dir disposes.
        var act = () => new Translator(NewSupportedLocales(), dir.Path);

        act.Should().Throw<System.Text.Json.JsonException>();
    }

    // ----------------------------------------------------------------------
    // T(locale, message) — basic lookup
    // ----------------------------------------------------------------------

    [Fact]
    public void T_KnownKeyInRequestedLocale_ReturnsTranslation()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        translator.T("en-US", TK.Common.Errors.NOT_FOUND).Should().Be("Not found.");
    }

    [Fact]
    public void T_UnknownKey_ReturnsRawKeyAsFallback()
    {
        // Documented contract: T NEVER throws on missing key. Raw key string
        // is dev-readable and serves as a useful debugging signal.
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var unknown = TK.Common.Errors.UNAUTHORIZED;
        translator.T("en-US", unknown).Should().Be(unknown.Key);
    }

    [Fact]
    public void T_KeyMissingInRequestedLocale_FallsBackToBase()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });
        dir.Write("fr-FR.json", new() { ["common_errors_FORBIDDEN"] = "Interdit." });

        var sl = NewSupportedLocales("en-US", "fr-FR");
        var translator = new Translator(sl, dir.Path);

        // fr-FR doesn't have NOT_FOUND → falls back to en-US.
        translator.T("fr-FR", TK.Common.Errors.NOT_FOUND).Should().Be("Not found.");
    }

    [Fact]
    public void T_BareLanguageCode_ResolvesViaSupportedLocales()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "EN: Not found." });
        dir.Write("fr-FR.json", new() { ["common_errors_NOT_FOUND"] = "FR: Pas trouvé." });

        var sl = NewSupportedLocales("en-US", "fr-FR");
        var translator = new Translator(sl, dir.Path);

        // "fr" → resolves to "fr-FR" via LanguageDefaults.
        translator.T("fr", TK.Common.Errors.NOT_FOUND).Should().Be("FR: Pas trouvé.");
    }

    [Fact]
    public void T_UnknownLocale_FallsBackToBase()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        // zh-CN unknown → SupportedLocales.Resolve falls back to en-US.
        translator.T("zh-CN", TK.Common.Errors.NOT_FOUND).Should().Be("Not found.");
    }

    [Fact]
    public void T_NullLocale_Throws()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var act = () => translator.T(null!, TK.Common.Errors.NOT_FOUND);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void T_EmptyOrWhitespaceLocale_Throws(string locale)
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var act = () => translator.T(locale, TK.Common.Errors.NOT_FOUND);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void T_NullMessage_Throws()
    {
        using var dir = new TempCatalog();
        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var act = () => translator.T("en-US", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // ----------------------------------------------------------------------
    // Parameter interpolation
    // ----------------------------------------------------------------------

    [Fact]
    public void T_WithSingleParam_SubstitutesPlaceholder()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found: {entity}" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var msg = TK.Common.Errors.NOT_FOUND.With("entity", "user");
        translator.T("en-US", msg).Should().Be("Not found: user");
    }

    [Fact]
    public void T_WithMultipleParams_SubstitutesAll()
    {
        using var dir = new TempCatalog();
        dir.Write(
            "en-US.json",
            new() { ["common_errors_NOT_FOUND"] = "{kind} {entity} (id: {id})" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var msg = TK.Common.Errors.NOT_FOUND
            .With("kind", "User")
            .With("entity", "account")
            .With("id", "42");
        translator.T("en-US", msg).Should().Be("User account (id: 42)");
    }

    [Fact]
    public void T_PlaceholderAppearsTwice_BothReplaced()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "{name}/{name}" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        translator.T("en-US", TK.Common.Errors.NOT_FOUND.With("name", "x")).Should().Be("x/x");
    }

    [Fact]
    public void T_UnmatchedPlaceholder_LeftLiteral()
    {
        // Adversarial: a template referencing {missing} when no such param is
        // bound leaves the placeholder verbatim. Translator does NOT throw.
        using var dir = new TempCatalog();
        dir.Write(
            "en-US.json",
            new() { ["common_errors_NOT_FOUND"] = "Not found: {entity} ({missing})" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var msg = TK.Common.Errors.NOT_FOUND.With("entity", "user");
        translator.T("en-US", msg).Should().Be("Not found: user ({missing})");
    }

    [Fact]
    public void T_ExtraParamNotInTemplate_Ignored()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found: {entity}" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var msg = TK.Common.Errors.NOT_FOUND
            .With("entity", "user")
            .With("unused", "ignored");
        translator.T("en-US", msg).Should().Be("Not found: user");
    }

    [Fact]
    public void T_ParamValueContainsBraces_NoRecursiveInterpolation()
    {
        // Adversarial: substitution is single-pass. A param value that itself
        // looks like a placeholder is inserted verbatim — no recursive expansion.
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "{x}" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var msg = TK.Common.Errors.NOT_FOUND
            .With("x", "{evil}")
            .With("evil", "should-not-appear");
        translator.T("en-US", msg).Should().Be("{evil}");
    }

    [Fact]
    public void T_NoParamsBound_TemplateUsedVerbatim()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found: {x}" });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        // No With() — Parameters is null. Template returned with placeholder intact.
        translator.T("en-US", TK.Common.Errors.NOT_FOUND).Should().Be("Not found: {x}");
    }

    // ----------------------------------------------------------------------
    // HasKey
    // ----------------------------------------------------------------------

    [Fact]
    public void HasKey_KeyInAnyLocale_ReturnsTrue()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });
        dir.Write("fr-FR.json", new() { ["common_errors_NOT_FOUND"] = "Pas trouvé." });

        var translator = new Translator(NewSupportedLocales("en-US", "fr-FR"), dir.Path);

        translator.HasKey("common_errors_NOT_FOUND").Should().BeTrue();
    }

    [Fact]
    public void HasKey_KeyOnlyInBaseLocale_ReturnsTrue()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });
        dir.Write("fr-FR.json", new() { ["common_errors_FORBIDDEN"] = "Interdit." });

        var translator = new Translator(NewSupportedLocales("en-US", "fr-FR"), dir.Path);

        translator.HasKey("common_errors_NOT_FOUND").Should().BeTrue();
    }

    [Fact]
    public void HasKey_UnknownKey_ReturnsFalse()
    {
        using var dir = new TempCatalog();
        dir.Write("en-US.json", new() { ["common_errors_NOT_FOUND"] = "Not found." });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        translator.HasKey("does_not_exist").Should().BeFalse();
    }

    [Fact]
    public void HasKey_Null_Throws()
    {
        using var dir = new TempCatalog();
        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var act = () => translator.HasKey(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void HasKey_EmptyOrWhitespace_Throws(string key)
    {
        using var dir = new TempCatalog();
        var translator = new Translator(NewSupportedLocales(), dir.Path);

        var act = () => translator.HasKey(key);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void HasKey_SchemaKey_NotConsideredAKey()
    {
        // $schema is stripped from every loaded catalog at load time.
        using var dir = new TempCatalog();
        dir.Write(
            "en-US.json",
            new()
            {
                ["$schema"] = "./schema.json",
                ["common_errors_NOT_FOUND"] = "Not found.",
            });

        var translator = new Translator(NewSupportedLocales(), dir.Path);

        translator.HasKey("$schema").Should().BeFalse();
        translator.HasKey("common_errors_NOT_FOUND").Should().BeTrue();
    }

    // ----------------------------------------------------------------------
    // Concurrency — Translator is registered as a process-wide singleton
    // ----------------------------------------------------------------------

    [Fact]
    public async Task T_ConcurrentCallers_ProduceConsistentResults()
    {
        // Adversarial: 100 concurrent callers across 2 locales × 2 keys.
        // Verifies the in-memory dictionaries are read-only after construction
        // and the regex interpolator is thread-safe.
        using var dir = new TempCatalog();
        dir.Write(
            "en-US.json",
            new()
            {
                ["common_errors_NOT_FOUND"] = "EN: Not found {x}",
                ["common_errors_FORBIDDEN"] = "EN: Forbidden",
            });
        dir.Write(
            "fr-FR.json",
            new()
            {
                ["common_errors_NOT_FOUND"] = "FR: Pas trouvé {x}",
                ["common_errors_FORBIDDEN"] = "FR: Interdit",
            });

        var translator = new Translator(NewSupportedLocales("en-US", "fr-FR"), dir.Path);

        const int concurrent_callers = 100;
        var tasks = Enumerable.Range(0, concurrent_callers).Select(i => Task.Run(() =>
        {
            var locale = i % 2 == 0 ? "en-US" : "fr-FR";
            var iAsText = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            var msg = ((i / 2) % 2) == 0
                ? TK.Common.Errors.NOT_FOUND.With("x", iAsText)
                : TK.Common.Errors.FORBIDDEN;
            return (locale, i, translator.T(locale, msg));
        }));

        var results = await Task.WhenAll(tasks);

        foreach (var (locale, i, value) in results)
        {
            var prefix = locale == "en-US" ? "EN" : "FR";
            var notFoundText = prefix == "EN" ? "Not found" : "Pas trouvé";
            if (((i / 2) % 2) == 0)
            {
                value.Should().Be($"{prefix}: {notFoundText} {i}");
            }
            else
            {
                value.Should().Be($"{prefix}: {(prefix == "EN" ? "Forbidden" : "Interdit")}");
            }
        }
    }

    // ----------------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------------

    private static SupportedLocales NewSupportedLocales(params string[] locales)
    {
        if (locales.Length == 0)
        {
            return new SupportedLocales(new ConfigurationBuilder().Build());
        }

        var dict = new Dictionary<string, string?>();
        for (var i = 0; i < locales.Length; i++)
        {
            dict[$"PUBLIC_ENABLED_LOCALES:{i}"] = locales[i];
        }

        return new SupportedLocales(
            new ConfigurationBuilder().AddInMemoryCollection(dict).Build());
    }

    /// <summary>
    /// Disposable per-test catalog directory. Lives under the system temp dir,
    /// auto-deleted on dispose.
    /// </summary>
    [MustDisposeResource]
    private sealed class TempCatalog : IDisposable
    {
        public TempCatalog()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "d2-i18n-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Write(string fileName, Dictionary<string, string> entries)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(entries);
            File.WriteAllText(System.IO.Path.Combine(Path, fileName), json);
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (DirectoryNotFoundException)
            {
                // already gone
            }
            catch (IOException)
            {
                // file lock during parallel test cleanup — best-effort cleanup
            }
        }
    }
}
