// -----------------------------------------------------------------------
// <copyright file="OtelMessagingTagEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.OtelMessagingTags.SourceGen;

/// <summary>
/// One OTel activity-tag entry parsed from
/// <c>contracts/otel-messaging-tags/otel-messaging-tags.spec.json</c>.
/// </summary>
/// <param name="ConstName">
/// UPPER_SNAKE_CASE C# / TS constant identifier (e.g. <c>MESSAGING_SYSTEM</c>).
/// Becomes the public field name on the emitted static class.
/// </param>
/// <param name="Value">
/// Wire-format OTel attribute name emitted on the Activity
/// (e.g. <c>messaging.system</c>, <c>messaging.operation.type</c>).
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record OtelMessagingTagEntry(string ConstName, string Value, string Doc);
