// -----------------------------------------------------------------------
// <copyright file="PingAuditHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Audit.App.Application.Handlers.Queries.PingAudit;

using DcsvIo.D2.Handler;
using DcsvIo.D2.Result;
using H = DcsvIo.D2.Private.Audit.App.Application.Handlers.Queries.PingAudit.IPingAuditHandler;
using I = DcsvIo.D2.Private.Audit.Client.Ping.PingAuditInput;
using O = DcsvIo.D2.Private.Audit.Client.Ping.PingAuditOutput;

/// <summary>
/// Multiproc stub NIE handler for <c>PingAudit</c>. Transport already
/// established origin (CrossProcessHop from Edge). Returns typed
/// <see cref="D2Result{TData}.ServiceUnavailable"/> — never throws
/// <c>NotImplementedException</c>.
/// </summary>
public sealed class PingAuditHandler(HandlerContext<PingAuditHandler> ctx)
    : BaseHandler<PingAuditHandler, I, O>(ctx), H
{
    /// <inheritdoc/>
    protected override ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // Empty input marker — no Domain.Create required.
        // Typed not-implemented: product Audit store is out of this multiproc stub.
        return ValueTask.FromResult(D2Result<O?>.ServiceUnavailable());
    }
}
