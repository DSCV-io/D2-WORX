// -----------------------------------------------------------------------
// <copyright file="AnonymizationClassifierSerialCollectionDefinition.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Tests.Unit.DataGovernance;

using Xunit;

/// <summary>
/// xUnit collection definition that serializes the four test classes that
/// call <c>AnonymizationTierClassifier.ClearCache()</c> and assert
/// exact <c>CacheCount</c> values against the process-global static
/// <c>ConcurrentDictionary</c> inside <c>AnonymizationTierClassifier</c>.
/// <para>
/// <c>DisableParallelization = true</c> prevents a parallel
/// <c>ClearCache()</c> from another class in this collection from racing
/// a concurrent <c>Classify()</c> call and corrupting the count observed
/// by another class's assertion. The EF Core model is immutable after
/// build and a singleton per <c>DbContext</c> type, so the process-global
/// cache is correct in production (where no test-only <c>ClearCache()</c>
/// calls occur); serializing only the test classes that mutate it is the
/// minimal correct fix.
/// </para>
/// </summary>
[CollectionDefinition("AnonymizationClassifierSerial", DisableParallelization = true)]
public sealed class AnonymizationClassifierSerialCollectionDefinition
{
}
