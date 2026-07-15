// -----------------------------------------------------------------------
// <copyright file="FakeSignWithKindFixtureHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Unit.KeyCustodian.TypeSpecGrpcEnum;

using DcsvIo.D2.Handler.Abstractions;
using DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcEnum.Generated;
using DcsvIo.D2.Result;

/// <summary>
/// In-process stand-in for <see cref="ISignWithKindFixtureHandler"/> used by
/// <see cref="EnumWireRoundTripTests"/>. Records the last input so the test can
/// assert the proto-string key_kind parsed back to the correct C# enum, and
/// counts calls so the inbound-fail-loud test can prove the handler is NOT
/// invoked when the wire enum value is unknown (the mapper short-circuits first).
/// </summary>
internal sealed class FakeSignWithKindFixtureHandler(D2Result<SignWithKindFixtureOutput?> result)
    : ISignWithKindFixtureHandler
{
    internal SignWithKindFixtureInput? LastInput { get; private set; }

    internal int CallCount { get; private set; }

    /// <inheritdoc/>
    public ValueTask<D2Result<SignWithKindFixtureOutput?>> HandleAsync(
        SignWithKindFixtureInput input,
        CancellationToken ct = default,
        HandlerOptions? options = null)
    {
        LastInput = input;
        CallCount++;
        return ValueTask.FromResult(result);
    }
}
