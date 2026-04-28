// -----------------------------------------------------------------------
// <copyright file="GeoDomainExceptionTests.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Geo.Tests.Unit.Domain.Exceptions;

using D2.Geo.Domain.Exceptions;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="GeoDomainException"/> and derived types.
/// </summary>
public class GeoDomainExceptionTests
{
    /// <summary>
    /// Tests that GeoValidationException formats message correctly.
    /// </summary>
    [Fact]
    public void GeoValidationException_FormatsMessageCorrectly()
    {
        // Act
        var exception = new GeoValidationException(
            "TestEntity",
            "TestProperty",
            "invalid-value",
            "is invalid.");

        // Assert
        exception.Message.Should().Contain("TestEntity");
        exception.Message.Should().Contain("TestProperty");
        exception.Message.Should().Contain("invalid-value");
        exception.Message.Should().Contain("is invalid.");
        exception.ObjectTypeName.Should().Be("TestEntity");
        exception.PropertyName.Should().Be("TestProperty");
        exception.InvalidValue.Should().Be("invalid-value");
        exception.Reason.Should().Be("is invalid.");
    }

    /// <summary>
    /// Tests that GeoValidationException handles null invalid value.
    /// </summary>
    [Fact]
    public void GeoValidationException_WithNullInvalidValue_FormatsCorrectly()
    {
        // Act
        var exception = new GeoValidationException(
            "TestEntity",
            "TestProperty",
            null,
            "cannot be null.");

        // Assert
        exception.InvalidValue.Should().BeNull();
        exception.Message.Should().Contain("''");
    }

    /// <summary>
    /// Tests the inner exception constructor path by creating a test-only derived exception.
    /// </summary>
    [Fact]
    public void GeoDomainException_WithInnerException_PreservesInnerException()
    {
        // Arrange
        var innerException = new InvalidOperationException("Inner error");

        // Act
        var exception = new TestGeoDomainException("Outer error", innerException);

        // Assert
        exception.Message.Should().Be("Outer error");
        exception.InnerException.Should().Be(innerException);
        exception.InnerException!.Message.Should().Be("Inner error");
    }

    /// <summary>
    /// Test-only derived exception to cover the inner exception constructor path.
    /// </summary>
    private sealed class TestGeoDomainException : GeoDomainException
    {
        public TestGeoDomainException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
