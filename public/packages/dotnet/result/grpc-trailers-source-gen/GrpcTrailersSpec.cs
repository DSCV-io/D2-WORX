// -----------------------------------------------------------------------
// <copyright file="GrpcTrailersSpec.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Grpc.Trailers.SourceGen;

using System.Collections.Immutable;

/// <summary>
/// Parsed shape of <c>contracts/grpc-trailers/grpc-trailers.spec.json</c>.
/// The <c>$schema</c> field is intentionally absent — JSON-Schema validation
/// happens at edit time in editors / IDEs; the loader just deserializes the
/// data fields and validates them in <see cref="GrpcTrailersEmitter"/>.
/// </summary>
/// <param name="Trailers">Every trailer-key entry declared in the spec (in spec order).</param>
internal sealed record GrpcTrailersSpec(ImmutableArray<GrpcTrailerEntry> Trailers);
