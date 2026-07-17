// -----------------------------------------------------------------------
// <copyright file="ValidationCorpus.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.Validation;

/// <summary>A parsed validation parity corpus.</summary>
internal sealed class ValidationCorpus
{
    /// <summary>Gets the corpus schema version.</summary>
    public string Version { get; init; } = string.Empty;

    /// <summary>Gets the validator the corpus targets (<c>email</c> etc.).</summary>
    public string Validator { get; init; } = string.Empty;

    /// <summary>Gets the corpus rows.</summary>
    public List<ValidationRow> Rows { get; } = [];
}
