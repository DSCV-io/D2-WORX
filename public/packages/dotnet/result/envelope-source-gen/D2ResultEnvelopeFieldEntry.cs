// -----------------------------------------------------------------------
// <copyright file="D2ResultEnvelopeFieldEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Result.Envelope.SourceGen;

/// <summary>
/// One field-name entry from the <c>fields[]</c> catalog of
/// <c>d2result-envelope.spec.json</c>.
/// </summary>
/// <param name="ConstName">UPPER_SNAKE_CASE C# / TS constant identifier.</param>
/// <param name="Value">Wire-format JSON property name (camelCase).</param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record D2ResultEnvelopeFieldEntry(string ConstName, string Value, string Doc);
