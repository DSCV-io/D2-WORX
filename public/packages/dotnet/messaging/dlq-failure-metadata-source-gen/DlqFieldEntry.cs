// -----------------------------------------------------------------------
// <copyright file="DlqFieldEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Messaging.DlqMetadata.SourceGen;

/// <summary>
/// One field-name entry from the <c>fields[]</c> sub-catalog of
/// <c>dlq-failure-metadata.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Value">Wire-format JSON property name.</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record DlqFieldEntry(string ConstName, string Value, string Doc);
