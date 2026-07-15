// -----------------------------------------------------------------------
// <copyright file="IExemptFromAnonymization.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.DataGovernance.Abstractions;

/// <summary>
/// Marks a financially or legally retained entity that the anonymization engine skips
/// entirely. No field on an exempt entity is ever overwritten — even if decorated with
/// <see cref="AnonymizableAttribute"/> — when an erasure sweep runs.
/// </summary>
/// <remarks>
/// This interface is the escape hatch for records that must be preserved for compliance
/// reasons (e.g. invoices, audit logs, legal holds). Apply it to the entity class; the
/// engine detects it at model-build time and records every such entity type in
/// <see cref="AnonymizationOutcome.EntityTypesSkippedExempt"/>.
///
/// Style mirrors the caching marker interfaces (<c>ILocalCache</c>, <c>IDistributedCache</c>
/// etc.) — an empty marker with semantic meaning carried by the name alone.
/// </remarks>
public interface IExemptFromAnonymization
{
}
