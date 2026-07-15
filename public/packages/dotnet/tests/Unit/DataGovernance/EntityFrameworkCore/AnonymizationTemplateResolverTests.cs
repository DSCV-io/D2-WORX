// -----------------------------------------------------------------------
// <copyright file="AnonymizationTemplateResolverTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

/// <summary>
/// Tests for <see cref="AnonymizationTemplateResolver"/>. Parse tests are pure-string;
/// validate and resolve tests build a tiny Npgsql model (model-build-only — no live connection).
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizationTemplateResolverTests
{
    // =========================================================================
    // AnonymizationTemplatePlan / AnonymizationTemplateSegment record round-trips
    // =========================================================================

    [Fact]
    public void AnonymizationTemplatePlan_record_equality_and_with_mutation()
    {
        var plan = new AnonymizationTemplatePlan
        {
            RawTemplate = "a{X}b",
            Segments = Array.Empty<AnonymizationTemplateSegment>(),
            TokenNames = Array.Empty<string>(),
        };

        var plan2 = plan with { RawTemplate = "other" };
        plan2.RawTemplate.Should().Be("other");
        plan.Should().NotBe(plan2);
    }

    [Fact]
    public void AnonymizationTemplateSegment_record_equality_and_with_mutation()
    {
        var seg = new AnonymizationTemplateSegment { IsToken = true, Text = "UserId" };
        var seg2 = seg with { Text = "OrgId" };
        seg2.Text.Should().Be("OrgId");
        seg.Should().NotBe(seg2);
    }

    // =========================================================================
    // Parse — empty string
    // =========================================================================

    [Fact]
    public void Parse_empty_string_produces_zero_segments_and_zero_token_names()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse(string.Empty);

        plan.Segments.Should().BeEmpty();
        plan.TokenNames.Should().BeEmpty();
        plan.RawTemplate.Should().Be(string.Empty);
    }

    // =========================================================================
    // Parse — single token
    // =========================================================================

    [Fact]
    public void Parse_single_token_produces_3_segments_and_1_token_name()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("a{X}b");

        plan.Segments.Count.Should().Be(3);
        plan.Segments[0].IsToken.Should().BeFalse();
        plan.Segments[0].Text.Should().Be("a");
        plan.Segments[1].IsToken.Should().BeTrue();
        plan.Segments[1].Text.Should().Be("X");
        plan.Segments[2].IsToken.Should().BeFalse();
        plan.Segments[2].Text.Should().Be("b");

        plan.TokenNames.Count.Should().Be(1);
        plan.TokenNames[0].Should().Be("X");
    }

    // =========================================================================
    // Parse — multiple tokens
    // =========================================================================

    [Fact]
    public void Parse_multiple_tokens_produces_correct_segment_list()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("x{A}y{B}z");

        plan.Segments.Count.Should().Be(5);
        plan.Segments[0].Text.Should().Be("x");
        plan.Segments[1].Text.Should().Be("A");
        plan.Segments[1].IsToken.Should().BeTrue();
        plan.Segments[2].Text.Should().Be("y");
        plan.Segments[3].Text.Should().Be("B");
        plan.Segments[3].IsToken.Should().BeTrue();
        plan.Segments[4].Text.Should().Be("z");

        plan.TokenNames.Should().BeEquivalentTo(new[] { "A", "B" });
    }

    // =========================================================================
    // Parse — adjacent tokens (no literal between)
    // =========================================================================

    [Fact]
    public void Parse_adjacent_tokens_produces_exactly_2_token_segments_and_no_literal_between()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("{A}{B}");

        plan.Segments.Count.Should().Be(2);
        plan.Segments[0].IsToken.Should().BeTrue();
        plan.Segments[0].Text.Should().Be("A");
        plan.Segments[1].IsToken.Should().BeTrue();
        plan.Segments[1].Text.Should().Be("B");

        plan.TokenNames.Count.Should().Be(2);
    }

    // =========================================================================
    // Parse — no tokens
    // =========================================================================

    // long identifier — cannot wrap
    [Fact]
    public void Parse_template_with_no_tokens_produces_single_literal_segment_and_empty_token_names()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("plainliteral");

        plan.Segments.Count.Should().Be(1);
        plan.Segments[0].IsToken.Should().BeFalse();
        plan.Segments[0].Text.Should().Be("plainliteral");
        plan.TokenNames.Should().BeEmpty();
    }

    // =========================================================================
    // Parse — literal brace escape {{ and }}
    // =========================================================================

    [Fact]
    public void Parse_double_open_brace_produces_literal_open_brace_in_segment()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("a{{b}}c");

        // "a{{b}}c" → literal "a{b}c"
        plan.Segments.Count.Should().Be(1);
        plan.Segments[0].IsToken.Should().BeFalse();
        plan.Segments[0].Text.Should().Be("a{b}c");
        plan.TokenNames.Should().BeEmpty();
    }

    [Fact]
    public void Parse_RawTemplate_preserves_original_string_including_escape_sequences()
    {
        const string template = "a{{b}}c";
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse(template);

        plan.RawTemplate.Should().Be(template);
    }

    // long identifier — cannot wrap
    [Fact]
    public void Parse_token_followed_by_double_close_brace_escape_produces_token_and_literal_close_brace()
    {
        // "{Field}}}suffix" → token "Field" + literal "}" + literal "suffix"
        // The first "}" (position 6) closes the token; "}}" at positions 7-8 is the
        // literal-brace escape producing one "}"; "suffix" is appended as literal.
        // This is the string.Format convention: {token}}} = {token} + }} = value + }.
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("{Field}}}suffix");

        plan.TokenNames.Should().ContainSingle().Which.Should().Be("Field");

        // Two segments: token "Field" then literal "}suffix"
        plan.Segments.Count.Should().Be(2);
        plan.Segments[0].IsToken.Should().BeTrue();
        plan.Segments[0].Text.Should().Be("Field");
        plan.Segments[1].IsToken.Should().BeFalse();
        plan.Segments[1].Text.Should().Be("}suffix");
    }

    // =========================================================================
    // Parse — malformed / adversarial inputs
    // =========================================================================

    [Fact]
    public void Parse_unmatched_open_brace_throws_ArgumentException()
    {
        var act = () => AnonymizationTemplateResolver.Parse("a{b");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_stray_close_brace_throws_ArgumentException()
    {
        var act = () => AnonymizationTemplateResolver.Parse("a}b");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_empty_token_throws_ArgumentException()
    {
        var act = () => AnonymizationTemplateResolver.Parse("{}");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Parse_null_template_throws_ArgumentNullException()
    {
        var act = () => AnonymizationTemplateResolver.Parse(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // ValidateTokens — valid token
    // =========================================================================

    [Fact]
    public void ValidateTokens_valid_scalar_sibling_returns_empty_bad_token_list()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        var plan = AnonymizationTemplateResolver.Parse(
            "deletedUser{UserId}@deleted.user.dcsv.io");

        var badTokens = AnonymizationTemplateResolver.ValidateTokens(plan, entityType);

        badTokens.Should().BeEmpty();
    }

    // =========================================================================
    // ValidateTokens — missing sibling (adversarial)
    // =========================================================================

    [Fact]
    public void ValidateTokens_nonexistent_sibling_name_appears_in_bad_token_list()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        var plan = AnonymizationTemplateResolver.Parse("{DoesNotExist}");

        var badTokens = AnonymizationTemplateResolver.ValidateTokens(plan, entityType);

        badTokens.Should().Contain("DoesNotExist");
    }

    // =========================================================================
    // ValidateTokens — shadow property (adversarial)
    // =========================================================================

    [Fact]
    public void ValidateTokens_shadow_property_name_appears_in_bad_token_list()
    {
        using ShadowPropertyContext ctx = ShadowPropertyContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ShadowEntity))!;

        // "ShadowField" is a shadow property set up in OnModelCreating.
        var plan = AnonymizationTemplateResolver.Parse("{ShadowField}");

        var badTokens = AnonymizationTemplateResolver.ValidateTokens(plan, entityType);

        badTokens.Should().Contain("ShadowField");
    }

    // =========================================================================
    // ValidateTokens — navigation property token (adversarial)
    // =========================================================================

    [Fact]
    public void ValidateTokens_navigation_property_name_appears_in_bad_token_list()
    {
        using NavEntityContext ctx = NavEntityContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(NavOwnerEntity))!;

        // "Related" is a navigation property — not a scalar; ValidateTokens must reject it.
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("{Related}");

        var badTokens = AnonymizationTemplateResolver.ValidateTokens(plan, entityType);

        // FindProperty returns null for navigations → token lands in bad-token list.
        badTokens.Should().Contain("Related");
    }

    // =========================================================================
    // ValidateTokens — null guard
    // =========================================================================

    [Fact]
    public void ValidateTokens_null_plan_throws_ArgumentNullException()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;

        var act = () => AnonymizationTemplateResolver.ValidateTokens(null!, entityType);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ValidateTokens_null_entity_type_throws_ArgumentNullException()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("a{X}b");

        var act = () => AnonymizationTemplateResolver.ValidateTokens(plan, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // Resolve — Guid sibling → 32-char lowercase no-dashes
    // =========================================================================

    [Fact]
    public void Resolve_guid_sibling_formats_as_32_char_lowercase_no_dashes()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("{UserId}");

        var instance = new ValidateEntity
        {
            Id = 1,
            UserId = new Guid("11223344-5566-7788-99aa-bbccddeeff00"),
            Name = "Alice",
        };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        // Guid.ToString("N") → lowercase, no dashes, 32 hex chars.
        result.Should().Be("112233445566778899aabbccddeeff00");
        result.Length.Should().Be(32);
        result.Should().NotContain("-");
        result.Should().Be(result.ToLowerInvariant()); // lowercase
    }

    // =========================================================================
    // Resolve — string sibling substituted verbatim
    // =========================================================================

    [Fact]
    public void Resolve_string_sibling_is_substituted_verbatim()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("{Name}");

        var instance = new ValidateEntity { Id = 1, UserId = Guid.NewGuid(), Name = "Alice Smith" };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        result.Should().Be("Alice Smith");
    }

    // =========================================================================
    // Resolve — multiple tokens in order
    // =========================================================================

    [Fact]
    public void Resolve_multiple_tokens_substituted_in_order()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        var plan = AnonymizationTemplateResolver.Parse("del{Name}_{UserId}@x");

        var guid = new Guid("aaaabbbbccccddddeeeeffffaaaabbbb");
        var instance = new ValidateEntity { Id = 1, UserId = guid, Name = "bob" };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        string expected_guid_part = guid.ToString("N");
        result.Should().Be($"delbob_{expected_guid_part}@x");
    }

    // =========================================================================
    // Resolve — the email sentinel pattern
    // =========================================================================

    [Fact]
    public void Resolve_email_sentinel_template_produces_correct_tombstone()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        var plan = AnonymizationTemplateResolver.Parse(
            "deletedUser{UserId}@deleted.user.dcsv.io");

        var guid = new Guid("11223344-5566-7788-99aa-bbccddeeff00");
        var instance = new ValidateEntity { Id = 1, UserId = guid, Name = "someone" };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        result.Should().Be("deletedUser112233445566778899aabbccddeeff00@deleted.user.dcsv.io");
    }

    // =========================================================================
    // Resolve — null sibling → empty substitution, no throw (adversarial)
    // =========================================================================

    [Fact]
    public void Resolve_null_nullable_string_sibling_substitutes_empty_string_without_throwing()
    {
        using NullableSiblingContext ctx = NullableSiblingContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(NullableSiblingEntity))!;
        var plan = AnonymizationTemplateResolver.Parse("prefix{NullableField}suffix");

        var instance = new NullableSiblingEntity { Id = 1, NullableField = null };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        // Null sibling → empty; no throw.
        result.Should().Be("prefixsuffix");
    }

    // =========================================================================
    // Resolve — non-Guid non-string (int) → invariant ToString
    // =========================================================================

    [Fact]
    public void Resolve_int_sibling_uses_invariant_culture_and_is_not_culture_dependent()
    {
        using IntSiblingContext ctx = IntSiblingContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(IntSiblingEntity))!;
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("count_{Count}");

        var instance = new IntSiblingEntity { Id = 1, Count = 42 };

        string result = AnonymizationTemplateResolver.Resolve(plan, entityType, instance);

        // Invariant: 42 renders as "42" regardless of culture.
        result.Should().Be("count_42");
    }

    // =========================================================================
    // Resolve — null guards
    // =========================================================================

    [Fact]
    public void Resolve_null_plan_throws_ArgumentNullException()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;

        var act = () => AnonymizationTemplateResolver.Resolve(
            null!,
            entityType,
            new ValidateEntity());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_null_entity_type_throws_ArgumentNullException()
    {
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("a{X}b");
        var act = () => AnonymizationTemplateResolver.Resolve(plan, null!, new ValidateEntity());

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Resolve_null_entity_instance_throws_ArgumentNullException()
    {
        using ValidateContext ctx = ValidateContext.Build();
        IEntityType entityType = ctx.Model.FindEntityType(typeof(ValidateEntity))!;
        AnonymizationTemplatePlan plan = AnonymizationTemplateResolver.Parse("a{UserId}b");

        var act = () => AnonymizationTemplateResolver.Resolve(plan, entityType, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    // =========================================================================
    // Entity types and DbContexts
    // =========================================================================

    private sealed class ValidateEntity
    {
        public int Id { get; set; }

        public Guid UserId { get; set; }

        public string Name { get; set; } = string.Empty;
    }

    private sealed class ValidateContext : DbContext
    {
        private ValidateContext(DbContextOptions<ValidateContext> options)
            : base(options)
        {
        }

        public static ValidateContext Build()
        {
            var options = new DbContextOptionsBuilder<ValidateContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new ValidateContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ValidateEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class ShadowEntity
    {
        public int Id { get; set; }
    }

    private sealed class ShadowPropertyContext : DbContext
    {
        private ShadowPropertyContext(DbContextOptions<ShadowPropertyContext> options)
            : base(options)
        {
        }

        public static ShadowPropertyContext Build()
        {
            var options = new DbContextOptionsBuilder<ShadowPropertyContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new ShadowPropertyContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<ShadowEntity>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property<string>("ShadowField"); // shadow property — no CLR member
            });
        }
    }

    private sealed class NullableSiblingEntity
    {
        public int Id { get; set; }

        public string? NullableField { get; set; }
    }

    private sealed class NullableSiblingContext : DbContext
    {
        private NullableSiblingContext(DbContextOptions<NullableSiblingContext> options)
            : base(options)
        {
        }

        public static NullableSiblingContext Build()
        {
            var options = new DbContextOptionsBuilder<NullableSiblingContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new NullableSiblingContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NullableSiblingEntity>(e => e.HasKey(x => x.Id));
        }
    }

    private sealed class IntSiblingEntity
    {
        public int Id { get; set; }

        public int Count { get; set; }
    }

    private sealed class IntSiblingContext : DbContext
    {
        private IntSiblingContext(DbContextOptions<IntSiblingContext> options)
            : base(options)
        {
        }

        public static IntSiblingContext Build()
        {
            var options = new DbContextOptionsBuilder<IntSiblingContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new IntSiblingContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<IntSiblingEntity>(e => e.HasKey(x => x.Id));
        }
    }

    // -- Navigation property entity (for M-3: ValidateTokens rejects navigation token) --

    private sealed class NavRelatedEntity
    {
        public int Id { get; set; }

        public string Data { get; set; } = string.Empty;
    }

    private sealed class NavOwnerEntity
    {
        public int Id { get; set; }

        public int RelatedId { get; set; }

        public NavRelatedEntity Related { get; set; } = new();
    }

    private sealed class NavEntityContext : DbContext
    {
        private NavEntityContext(DbContextOptions<NavEntityContext> options)
            : base(options)
        {
        }

        public static NavEntityContext Build()
        {
            var options = new DbContextOptionsBuilder<NavEntityContext>()
                .UseNpgsql("Host=localhost;Database=govtest")
                .Options;
            return new NavEntityContext(options);
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<NavRelatedEntity>(e => e.HasKey(x => x.Id));
            model.Entity<NavOwnerEntity>(e =>
            {
                e.HasKey(x => x.Id);

                // "Related" is a navigation property — FindProperty returns null for navigations.
                e.HasOne(x => x.Related).WithMany().HasForeignKey(x => x.RelatedId);
            });
        }
    }
}
