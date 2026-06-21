// -----------------------------------------------------------------------
// <copyright file="FakeSignWithKindHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcEnum;

using D2.Edge.Tests.TypeSpecGrpcEnum.Generated;
using D2.Shared.Handler.Abstractions;
using D2.Shared.Result;

/// <summary>
/// In-process stand-in for <see cref="ISignWithKindHandler"/> used by
/// <see cref="EnumWireRoundTripTests"/>. Records the last input so the test can
/// assert the proto-string key_kind parsed back to the correct C# enum, and
/// counts calls so the inbound-fail-loud test can prove the handler is NOT
/// invoked when the wire enum value is unknown (the mapper short-circuits first).
/// </summary>
internal sealed class FakeSignWithKindHandler(D2Result<SignWithKindOutput?> result)
    : ISignWithKindHandler
{
    internal SignWithKindInput? LastInput { get; private set; }

    internal int CallCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<D2Result<SignWithKindOutput?>> HandleAsync(
        SignWithKindInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        LastInput = input;
        CallCount++;
        return ValueTask.FromResult(result);
    }
}
