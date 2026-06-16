// -----------------------------------------------------------------------
// <copyright file="FakeSignHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpc;

using D2.Edge.Tests.TypeSpecDto.Generated;
using D2.Edge.Tests.TypeSpecGrpc.Generated;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Result;

/// <summary>
/// In-process stand-in for <see cref="ISignHandler"/> used by
/// <see cref="GrpcServiceImplTests"/>.
/// </summary>
internal sealed class FakeSignHandler(D2Result<SignOutput?> result) : ISignHandler
{
    internal SignInput? LastInput { get; private set; }

    /// <inheritdoc/>
    public ValueTask<D2Result<SignOutput?>> HandleAsync(
        SignInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        LastInput = input;
        return ValueTask.FromResult(result);
    }
}
