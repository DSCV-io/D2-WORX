// -----------------------------------------------------------------------
// <copyright file="PingAuditHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Audit.App.Application.Handlers.Queries.PingAudit;

using D2.Shared.Handler;
using D2.Shared.Result;

using H = D2.Audit.App.Application.Handlers.Queries.PingAudit.IPingAuditHandler;
using I = D2.Audit.Client.Ping.PingAuditInput;
using O = D2.Audit.Client.Ping.PingAuditOutput;

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
