// -----------------------------------------------------------------------
// <copyright file="ISignHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

// Hand-written fixture interface for the TypeSpecGrpc in-memory harness.
// Mirrors the shape the gRPC service-impl emitter delegates to.
// The handler-interface emitter will replace this when it lands.

namespace D2.Edge.Tests.TypeSpecGrpc.Generated;

using D2.Shared.Result;
using DtoSignInput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignInput;
using DtoSignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;

/// <summary>Fixture handler interface for the <c>Sign</c> operation.</summary>
public interface ISignHandler
{
    Task<D2Result<DtoSignOutput>> HandleAsync(DtoSignInput input, CancellationToken ct);
}
