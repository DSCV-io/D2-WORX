// -----------------------------------------------------------------------
// <copyright file="IssueLeafHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf;

using D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueWorkloadCertificate;

using H = D2.Edge.KeyCustodian.App.Application.Handlers.Commands.IssueLeaf.IIssueLeafHandler;
using I = D2.Edge.KeyCustodian.Clients.IssueLeafInput;
using O = D2.Edge.KeyCustodian.Clients.IssueLeafOutput;

/// <summary>
/// Thin transport shell for the generated <c>issueLeaf</c> operation: maps the
/// flat wire DTO to the inner input, delegates to the kept hand-written
/// <see cref="IIssueWorkloadCertificateHandler"/> (the single chokepoint BOTH
/// planes flow through), and maps the inner output back to the wire DTO.
/// </summary>
/// <remarks>
/// Deliberately NO second gate and NO second telemetry site: the authority rule,
/// the scope requirement, the CSR verification, the audit write, and the deny
/// telemetry all live in the inner handler, so the two planes cannot drift. Inner
/// denials bubble unchanged (code + status preserved). Nothing on this path is
/// secret — CSR in, certificates out — so there is no redaction, no zeroing, and
/// no custody transfer.
/// </remarks>
public sealed class IssueLeafHandler(
    HandlerContext<IssueLeafHandler> ctx,
    IIssueWorkloadCertificateHandler inner)
    : BaseHandler<IssueLeafHandler, I, O>(ctx), H
{
    /// <inheritdoc/>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input, CancellationToken ct)
    {
        // Delegate through the inner handler's FULL pipeline (scope pre-check +
        // authority gate + CSR verification + audit) — the sibling-delegation
        // shape; the shell adds no gate of its own.
        var innerResult = await inner
            .HandleAsync(new IssueWorkloadCertificateInput(input.CsrDer), ct)
            .ConfigureAwait(false);

        if (!innerResult.Success || innerResult.Data is null)
            return D2Result<O?>.BubbleFail(innerResult);

        var certificate = innerResult.Data.Certificate;

        // Wire mapping: domain Instants → the DTO's DateTimeOffset wire form.
        return D2Result<O?>.Ok(new O(
            certificate.CertificateDer,
            certificate.IssuerCertificateDer,
            certificate.NotBefore.ToDateTimeOffset(),
            certificate.NotAfter.ToDateTimeOffset()));
    }
}
