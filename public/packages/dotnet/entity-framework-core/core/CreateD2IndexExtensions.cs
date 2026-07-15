// -----------------------------------------------------------------------
// <copyright file="CreateD2IndexExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EntityFrameworkCore;

using System;
using System.Linq.Expressions;
using DcsvIo.D2.Utilities.Extensions;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

/// <summary>
/// EF Core 10 migration helper for indexing <c>ComplexProperty</c> member columns.
/// </summary>
/// <remarks>
/// <para>
/// <b>EF Core 10 limitation</b> — a <c>ComplexProperty</c> member column CANNOT be
/// indexed model-aware in EF Core 10. Fluent <c>HasIndex(u =&gt; u.Vo.Member)</c>
/// throws ("not a valid member access expression"); <c>HasIndex("Vo_Member")</c>
/// throws (shadow property needs a type); metadata/convention index-adds are silently
/// discarded at finalization (<c>"index properties … not declared on the entity type"</c>)
/// — <c>IMigrationsModelDiffer</c> emits ZERO <c>CreateIndexOperation</c>. This was
/// empirically verified (EF Core 10.0.7, Npgsql 10.0.1) and is tracked as
/// <see href="https://github.com/dotnet/efcore/issues/31246">efcore #31246</see>.
/// </para>
/// <para>
/// <b>Workaround</b> — use <c>CreateD2Index</c> in the host's migration class. The
/// helper derives the <c>{ComplexProp}_{Member}</c> column name from the typed member
/// selector expression (model-unaware, matches EF Core 10 default complex column naming)
/// and emits a <c>CreateIndexOperation</c>.
/// </para>
/// <para>
/// <b>EF Core 11 native path</b> — <c>HasIndex(u =&gt; u.Vo.Member)</c> becomes native
/// in EF Core 11 (issue closed, milestone 11.0.0; PR #38192 merged 2026-05-19).
/// When the host upgrades to EF Core 11, move the index declaration to
/// <c>IEntityTypeConfiguration</c> and remove the <c>CreateD2Index</c> call from the
/// migration.
/// </para>
/// <para>
/// <b>Value-converter indexes have NO limitation</b> — value-converted single-value VO
/// columns (e.g. <c>EmailAddress</c>, <c>PhoneNumber</c>) are first-class and can be
/// indexed or made unique via <c>HasIndex(u =&gt; u.Email).IsUnique()</c> in the entity
/// configuration class (no workaround needed). Complex-member QUERIES (<c>WHERE
/// Location_City = 'X'</c>) also work fine — only the model-aware index declaration on
/// a complex member is limited.
/// </para>
/// <para>
/// <b>Column-name assumption</b> — <c>CreateD2Index</c> derives column names from the
/// expression member chain using EF Core 10 default complex column naming
/// (<c>{ComplexProp}_{Member}</c>, joined with <c>_</c>). A host that overrides complex
/// column prefixes via <c>HasColumnName</c> owns the index-column name too; the toolkit
/// helpers never call <c>HasColumnName</c>, so the default always holds for standard
/// D2 contact mappings.
/// </para>
/// </remarks>
public static class CreateD2IndexExtensions
{
    // =========================================================================
    // MigrationBuilder — CreateD2Index
    // =========================================================================
    extension(MigrationBuilder migrationBuilder)
    {
        /// <summary>
        /// Emits a <c>CREATE INDEX</c> operation on a complex-type member column.
        /// Derives the column name from the member-expression chain using EF Core 10
        /// default naming (<c>{ComplexProp}_{Member}</c>).
        /// </summary>
        /// <typeparam name="TEntity">The CLR type of the host entity.</typeparam>
        /// <param name="table">The physical table name the entity is mapped to.</param>
        /// <param name="member">
        /// Expression identifying the complex-type member to index.
        /// For a simple complex property: <c>u =&gt; u.Location.City</c> →
        /// column <c>Location_City</c>.
        /// </param>
        /// <param name="name">
        /// Optional index name. When <see langword="null"/> the default
        /// <c>IX_{table}_{Complex}_{Member}</c> is used.
        /// </param>
        /// <param name="unique">When <see langword="true"/> the index is unique.</param>
        /// <returns>
        /// An <see cref="OperationBuilder{CreateIndexOperation}"/> for fluent configuration
        /// of additional index options.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="table"/> or <paramref name="member"/> is
        /// <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// Thrown when <paramref name="table"/> is empty or whitespace.
        /// </exception>
        public OperationBuilder<CreateIndexOperation> CreateD2Index<TEntity>(
            string table,
            Expression<Func<TEntity, object?>> member,
            string? name = null,
            bool unique = false)
        {
            table.ThrowIfFalsey();

            // member is an Expression<> (plain reference type) — BCL ThrowIfNull applies;
            // uses ArgumentNullException.ThrowIfNull rather than ThrowIfFalsey because
            // there are no present-but-falsey semantics beyond null for a reference-type expression.
            ArgumentNullException.ThrowIfNull(member);

            var column = DeriveColumnName(member);
            var indexName = name ?? $"IX_{table}_{column}";
            return migrationBuilder.CreateIndex(
                name: indexName,
                table: table,
                column: column,
                unique: unique);
        }
    }

    // =========================================================================
    // Internal helpers
    // =========================================================================

    /// <summary>
    /// Walks the member-expression chain and joins the member names with <c>_</c> to
    /// produce the EF Core 10 default complex column name. The parameter node
    /// (outermost lambda parameter) is excluded from the chain.
    /// </summary>
    /// <typeparam name="TEntity">The CLR type of the host entity.</typeparam>
    /// <param name="member">
    /// The typed member-access expression (e.g. <c>u =&gt; u.Location.City</c>).
    /// </param>
    /// <returns>The derived column name (e.g. <c>Location_City</c>).</returns>
    internal static string DeriveColumnName<TEntity>(
        Expression<Func<TEntity, object?>> member)
    {
        var parts = new System.Collections.Generic.List<string>();
        Expression? current = member.Body;

        // Unwrap boxing conversion on value types.
        if (current is UnaryExpression { NodeType: ExpressionType.Convert } unary)
            current = unary.Operand;

        while (current is MemberExpression memberExpr)
        {
            parts.Add(memberExpr.Member.Name);
            current = memberExpr.Expression;
        }

        if (parts.Count == 0)
        {
            throw new ArgumentException(
                "The member expression must be a member access chain "
                + "(e.g. u => u.Location.City).",
                nameof(member));
        }

        // parts are collected innermost-first; reverse to get outermost-first.
        parts.Reverse();

        // Skip the parameter name (first element is the entity property, not the param).
        // For u => u.Location.City: parts = ["Location", "City"] (no param —
        // MemberExpression.Expression terminates at ParameterExpression, not a Member).
        return string.Join("_", parts);
    }
}
