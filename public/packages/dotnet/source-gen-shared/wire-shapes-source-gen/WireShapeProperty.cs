// -----------------------------------------------------------------------
// <copyright file="WireShapeProperty.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.WireShapes.SourceGen;

/// <summary>
/// One property-name entry parsed from a wire-shape spec
/// (<c>contracts/tk-message/tk-message.spec.json</c> or
/// <c>contracts/input-error/input-error.spec.json</c>).
/// </summary>
/// <param name="ConstName">
/// UPPER_SNAKE_CASE C# / TS constant identifier (e.g. <c>KEY</c>,
/// <c>PARAMS</c>, <c>FIELD</c>, <c>ERRORS</c>). Becomes the public field
/// name on the emitted static class.
/// </param>
/// <param name="Value">
/// Wire-format JSON property name emitted on the catalog's JSON object
/// (e.g. <c>key</c>, <c>params</c>, <c>field</c>, <c>errors</c>). The
/// literal IS the wire format.
/// </param>
/// <param name="Doc">
/// XML <c>summary</c> text rendered on the emitted constant.
/// </param>
internal sealed record WireShapeProperty(string ConstName, string Value, string Doc);
