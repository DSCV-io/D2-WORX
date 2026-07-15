// -----------------------------------------------------------------------
// <copyright file="SealedMessagingFixtureSubscriber.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian;

using DcsvIo.D2.Handler;
using I = DcsvIo.D2.Private.Edge.Tests.Integration.KeyCustodian.SealedMessagingFixtureEvent;
using O = DcsvIo.D2.Result.Unit;

/// <summary>
/// Fixture subscriber for the sealed-messaging headline integration test: captures
/// each opened sealed delivery's content into the <see cref="SealedDeliveryFixtureRecorder"/>.
/// A delivery can only reach this handler through the consumer-service-keyed
/// <c>IPayloadOpener</c> (the sealed decompose path), so its invocation IS the proof
/// the sealed frame was opened.
/// </summary>
public sealed class SealedMessagingFixtureSubscriber(
    HandlerContext<SealedMessagingFixtureSubscriber> ctx,
    SealedDeliveryFixtureRecorder recorder)
    : BaseHandler<SealedMessagingFixtureSubscriber, I, O>(ctx)
{
    /// <inheritdoc />
    // O is Unit (a value type), so the base's D2Result<TOutput?> resolves to
    // D2Result<Unit> — no nullable annotation on the alias.
    protected override ValueTask<D2Result<O>> ExecuteAsync(I input, CancellationToken ct)
    {
        recorder.Record(input.Content ?? string.Empty);

        return new ValueTask<D2Result<O>>(D2Result<O>.Ok(O.Value));
    }
}
