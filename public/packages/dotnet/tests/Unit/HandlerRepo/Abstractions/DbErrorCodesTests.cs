// -----------------------------------------------------------------------
// <copyright file="DbErrorCodesTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.HandlerRepo.Abstractions;

using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using DcsvIo.D2.Handler.Repo.Abstractions;
using Xunit;

/// <summary>
/// Pinning tests for <see cref="DbErrorCodes"/> — the constants are part of
/// the public wire contract (booleans match by string equality, not by symbol
/// reference). A silent rename would break consumers that hand-craft results
/// with these codes (middleware emitting from <see cref="DcsvIo.D2.Result.D2Result.Fail"/>
/// without referencing the abstractions package).
/// </summary>
public sealed class DbErrorCodesTests
{
    [Fact]
    public void Constants_ArePinnedToDocumentedStringValues()
    {
        DbErrorCodes.CONCURRENCY_CONFLICT.Should().Be("CONCURRENCY_CONFLICT");
        DbErrorCodes.UNIQUE_VIOLATION.Should().Be("UNIQUE_VIOLATION");
        DbErrorCodes.FOREIGN_KEY_VIOLATION.Should().Be("FOREIGN_KEY_VIOLATION");
        DbErrorCodes.NOT_NULL_VIOLATION.Should().Be("NOT_NULL_VIOLATION");
        DbErrorCodes.CHECK_VIOLATION.Should().Be("CHECK_VIOLATION");
        DbErrorCodes.DB_TIMEOUT.Should().Be("DB_TIMEOUT");
        DbErrorCodes.DB_DEADLOCK.Should().Be("DB_DEADLOCK");
        DbErrorCodes.DB_CONNECTION_FAILURE.Should().Be("DB_CONNECTION_FAILURE");
    }

    [Fact]
    public void Constants_AllScreamingSnakeCase()
    {
        // Adversarial: enforce naming convention. A new constant added in
        // camelCase / PascalCase would silently break the discriminator
        // contract (DbResultDbBooleans matches by literal string).
        var fields = typeof(DbErrorCodes).GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var f in fields.Where(f => f.IsLiteral && !f.IsInitOnly))
        {
            var value = (string)f.GetRawConstantValue()!;
            value.Should().MatchRegex(
                "^[A-Z][A-Z0-9_]*$",
                $"constant {f.Name} = '{value}' must be SCREAMING_SNAKE_CASE");
        }
    }

    [Fact]
    public void Constants_AllUnique()
    {
        // Adversarial: duplicate constant values would cause the wrong
        // boolean to fire on hand-crafted Fail results.
        var fields = typeof(DbErrorCodes).GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
        var values = fields
            .Where(f => f.IsLiteral && !f.IsInitOnly)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToArray();

        values.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Constants_NameAndValueMatch()
    {
        // Each constant is `nameof(NAME)` — value MUST equal the field
        // name. Documents that contract; a future hand-typed value that
        // accidentally diverges from the field name would trip this.
        var fields = typeof(DbErrorCodes).GetFields(
            BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        foreach (var f in fields.Where(f => f.IsLiteral && !f.IsInitOnly))
        {
            var value = (string)f.GetRawConstantValue()!;
            value.Should().Be(
                f.Name,
                $"constant {f.Name} should use nameof() — value '{value}' != name");
        }
    }
}
