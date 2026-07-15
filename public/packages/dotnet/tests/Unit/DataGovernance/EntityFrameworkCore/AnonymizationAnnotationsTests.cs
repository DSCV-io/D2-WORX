// -----------------------------------------------------------------------
// <copyright file="AnonymizationAnnotationsTests.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance.EntityFrameworkCore;

using AwesomeAssertions;
using DcsvIo.D2.DataGovernance.EntityFrameworkCore;
using Xunit;

/// <summary>
/// Pins the <see cref="AnonymizationAnnotations.ANONYMIZE"/> constant value. A rename of
/// the annotation key without updating the engine read-path would silently break
/// anonymization at runtime; this test makes that scenario a build-time failure.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AnonymizationAnnotationsTests
{
    [Fact]
    public void ANONYMIZE_constant_has_expected_literal_value()
    {
        AnonymizationAnnotations.ANONYMIZE.Should().Be("D2:Anonymize");
    }
}
