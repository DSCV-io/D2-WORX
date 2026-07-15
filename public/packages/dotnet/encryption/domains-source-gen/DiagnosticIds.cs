// -----------------------------------------------------------------------
// <copyright file="DiagnosticIds.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.EncryptionDomains.SourceGen;

/// <summary>Diagnostic IDs for encryption-domains source-gen.</summary>
internal static class DiagnosticIds
{
    /// <summary>Spec file is malformed JSON or violates the schema.</summary>
    public const string MalformedSpec = "D2ED001";

    /// <summary>Two entries share the same <c>constName</c>.</summary>
    public const string DuplicateConstName = "D2ED002";

    /// <summary>Two entries share the same wire <c>value</c>.</summary>
    public const string DuplicateValue = "D2ED003";

    /// <summary>Entry's <c>constName</c> doesn't match the UPPER_SNAKE_CASE pattern.</summary>
    public const string InvalidConstName = "D2ED004";

    /// <summary>Entry's <c>value</c> is empty or whitespace-only.</summary>
    public const string EmptyValue = "D2ED005";

    /// <summary>Entry's <c>mode</c> is neither <c>symmetric</c> nor <c>sealed</c>.</summary>
    public const string InvalidMode = "D2ED006";

    /// <summary>Entry has <c>mode: sealed</c> but no <c>consumerService</c>.</summary>
    public const string MissingConsumerService = "D2ED007";

    /// <summary>Entry has a <c>consumerService</c> but is not <c>mode: sealed</c>.</summary>
    public const string UnexpectedConsumerService = "D2ED008";

    /// <summary>Entry's <c>consumerService</c> violates the <c>[a-z0-9-]{1,64}</c> grammar.</summary>
    public const string InvalidConsumerService = "D2ED009";
}
