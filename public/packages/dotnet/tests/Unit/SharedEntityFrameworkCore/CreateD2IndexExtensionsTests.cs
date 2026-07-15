// -----------------------------------------------------------------------
// <copyright file="CreateD2IndexExtensionsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.SharedEntityFrameworkCore;

using System;
using AwesomeAssertions;
using DcsvIo.D2.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Xunit;

/// <summary>
/// Unit tests for <see cref="CreateD2IndexExtensions"/>.
/// Exercises every public + internal path via <c>MigrationBuilder</c>
/// and direct <c>DeriveColumnName</c> calls (model-free; no live DB).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CreateD2IndexExtensionsTests
{
    // =========================================================================
    // DeriveColumnName — column-name derivation
    // =========================================================================

    [Fact]
    public void DeriveColumnName_produces_Complex_Member_for_two_level_chain()
    {
        var column = CreateD2IndexExtensions.DeriveColumnName<IndexHostEntity>(
            u => u.Nested.Name);
        column.Should().Be("Nested_Name");
    }

    [Fact]
    public void DeriveColumnName_produces_property_name_for_single_level_access()
    {
        var column = CreateD2IndexExtensions.DeriveColumnName<IndexHostEntity>(
            u => u.Nested);
        column.Should().Be("Nested");
    }

    [Fact]
    public void DeriveColumnName_produces_three_level_chain()
    {
        var column = CreateD2IndexExtensions.DeriveColumnName<ThreeLevelEntity>(
            u => u.Outer.Inner.Value);
        column.Should().Be("Outer_Inner_Value");
    }

    [Fact]
    public void DeriveColumnName_unwraps_boxing_for_nullable_int_member()
    {
        // NullableCount is int? — the lambda boxes it into object?,
        // wrapping the member access in a Convert node the walker must unwrap.
        // Assign a value here so the set accessor is exercised (avoids unused-accessor
        // inspection warnings on test-only types).
        var vo = new NestedVo { NullableCount = 42 };
        _ = vo;
        var column = CreateD2IndexExtensions.DeriveColumnName<IndexHostEntity>(
            u => u.Nested.NullableCount);
        column.Should().Be("Nested_NullableCount");
    }

    [Fact]
    public void DeriveColumnName_throws_ArgumentException_for_non_member_expression()
    {
        var ex = Record.Exception(
            () => CreateD2IndexExtensions.DeriveColumnName<IndexHostEntity>(u => "literal"));
        ex.Should().BeOfType<ArgumentException>();
    }

    // =========================================================================
    // CreateD2Index — operation emission
    // =========================================================================

    [Fact]
    public void CreateD2Index_emits_default_named_non_unique_CreateIndexOperation()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        mb.CreateD2Index<IndexHostEntity>("entities", u => u.Nested.Name);

        mb.Operations.Should().ContainSingle();
        var op = mb.Operations[0].Should().BeOfType<CreateIndexOperation>().Subject;
        op.Table.Should().Be("entities");
        op.Columns.Should().NotBeEmpty();
        op.Columns[0].Should().Be("Nested_Name");
        op.Name.Should().Be("IX_entities_Nested_Name");
        op.IsUnique.Should().BeFalse();
    }

    [Fact]
    public void CreateD2Index_honors_supplied_name_and_unique_flag()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");

        mb.CreateD2Index<IndexHostEntity>(
            "entities",
            u => u.Nested.Name,
            name: "my_idx",
            unique: true);

        var op = mb.Operations[0].Should().BeOfType<CreateIndexOperation>().Subject;
        op.Name.Should().Be("my_idx");
        op.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void CreateD2Index_throws_ArgumentNullException_for_null_table()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var ex = Record.Exception(
            () => mb.CreateD2Index<IndexHostEntity>(null!, u => u.Nested.Name));
        ex.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void CreateD2Index_throws_ArgumentNullException_for_null_member()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var ex = Record.Exception(
            () => mb.CreateD2Index<IndexHostEntity>("t", null!));
        ex.Should().BeOfType<ArgumentNullException>();
    }

    [Fact]
    public void CreateD2Index_throws_ArgumentException_for_empty_table()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var ex = Record.Exception(
            () => mb.CreateD2Index<IndexHostEntity>(string.Empty, u => u.Nested.Name));
        ex.Should().BeOfType<ArgumentException>();
    }

    [Fact]
    public void CreateD2Index_throws_ArgumentException_for_whitespace_table()
    {
        var mb = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        var ex = Record.Exception(
            () => mb.CreateD2Index<IndexHostEntity>("   ", u => u.Nested.Name));
        ex.Should().BeOfType<ArgumentException>();
    }

    // =========================================================================
    // Test entities (self-contained — no Location/Contacts/Geo deps)
    // =========================================================================

    private sealed class NestedVo
    {
        public string Name { get; init; } = string.Empty;

        // int? so the lambda boxes it into object?, producing a Convert node
        // the DeriveColumnName walker must unwrap — same path as nullable enum/struct.
        public int? NullableCount { get; set; }
    }

    private sealed class IndexHostEntity
    {
        public NestedVo Nested { get; init; } = default!;
    }

    private sealed class InnerVo
    {
        public string Value { get; init; } = string.Empty;
    }

    private sealed class OuterVo
    {
        public InnerVo Inner { get; init; } = default!;
    }

    private sealed class ThreeLevelEntity
    {
        public OuterVo Outer { get; init; } = default!;
    }
}
