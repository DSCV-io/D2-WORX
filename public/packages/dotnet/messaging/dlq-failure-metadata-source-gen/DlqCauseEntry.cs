// -----------------------------------------------------------------------
// <copyright file="DlqCauseEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

/// <summary>
/// One cause-string entry from the <c>causes[]</c> sub-catalog of
/// <c>dlq-failure-metadata.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Value">Wire-format cause string.</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record DlqCauseEntry(string ConstName, string Value, string Doc);
