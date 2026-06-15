// -----------------------------------------------------------------------
// <copyright file="FakeSignHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Shared.Result;
using DtoSignInput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignInput;
using DtoSignOutput = global::D2.Edge.Tests.TypeSpecDto.Generated.SignOutput;

/// <summary>
/// In-process stand-in for <see cref="ISignHandler"/> used by
/// <see cref="GrpcServiceImplTests"/>.
/// </summary>
internal sealed class FakeSignHandler(D2Result<DtoSignOutput> result) : ISignHandler
{
    internal DtoSignInput? LastInput { get; private set; }

    /// <inheritdoc/>
    public Task<D2Result<DtoSignOutput>> HandleAsync(DtoSignInput input, CancellationToken ct)
    {
        LastInput = input;
        return Task.FromResult(result);
    }
}
