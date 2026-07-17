// -----------------------------------------------------------------------
// <copyright file="GrpcTrailerEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Grpc.Trailers.SourceGen;

/// <summary>
/// One gRPC trailer-key entry parsed from
/// <c>contracts/grpc-trailers/grpc-trailers.spec.json</c>.
/// </summary>
/// <param name="ConstName">
/// UPPER_SNAKE_CASE C# / TS constant identifier (e.g. <c>ERROR_CODE</c>,
/// <c>MESSAGES</c>, <c>TRACE_ID</c>). Becomes the public field name on the
/// emitted static class.
/// </param>
/// <param name="Value">
/// Wire-format trailer key emitted on the gRPC trailer Metadata
/// (e.g. <c>d2_error_code</c>, <c>d2_messages</c>, <c>traceId</c>). The
/// literal IS the wire format.
/// </param>
/// <param name="Doc">XML <c>summary</c> text rendered on the emitted constant.</param>
internal sealed record GrpcTrailerEntry(string ConstName, string Value, string Doc);
