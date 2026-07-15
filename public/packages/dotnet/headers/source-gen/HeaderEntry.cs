// -----------------------------------------------------------------------
// <copyright file="HeaderEntry.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Headers.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// One header entry parsed from
/// <c>contracts/headers/headers.spec.json</c>.
/// </summary>
/// <param name="Name">
/// Wire-format header name. Identical across every transport listed in
/// <paramref name="Applicability"/>.
/// </param>
/// <param name="ConstName">
/// Public C# constant identifier — UPPER_SNAKE_CASE.
/// </param>
/// <param name="Applicability">
/// Closed enum of transport bindings this header can appear on
/// (<c>http</c> / <c>grpc</c> / <c>amqp</c>).
/// </param>
/// <param name="Convention">
/// Provenance of the header naming convention. Documentation aid;
/// emitter surfaces in xmldoc.
/// </param>
/// <param name="Description">
/// Human-readable description of the header's purpose. Emitted as
/// xmldoc on every catalog the header appears in.
/// </param>
internal sealed record HeaderEntry(
    string Name,
    string ConstName,
    ImmutableArray<string> Applicability,
    string Convention,
    string Description);
