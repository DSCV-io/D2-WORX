// -----------------------------------------------------------------------
// <copyright file="PoCCsrSigningWorkloadCertificateIssuer.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.Api.Outbound;

using D2.Edge.Api.Composition;
using D2.Edge.KeyCustodian.App.Application.Issuance;
using D2.Edge.KeyCustodian.App.Infrastructure.Configuration;
using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Edge.KeyCustodian.Domain.ValueObjects;
using D2.Shared.Auth.Outbound.WorkloadCertificate;
using D2.Shared.Result;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NodaTime;

/// <summary>
/// Edge co-host <see cref="IWorkloadCertificateIssuer"/>: verifies a CSR, then
/// signs via the isolated <see cref="ICaLeafSigningCapability"/> for ServiceId
/// <see cref="EdgeHostIdentity.SERVICE_ID"/>. Returns cert-only
/// <see cref="WorkloadLeafMaterial"/> — the private key never crosses the seam.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not</b> <c>IIssueLeafHandler</c> / System-plane issuance (authority denies
/// System / InProcess for workload leaf minting). Mirrors the harness
/// <c>SeedingIssuer</c> path: <see cref="CsrVerification"/> + leaf capability.
/// </para>
/// <para>
/// Singleton (outbound stack requirement) but resolves the transient capability
/// (and scoped DbContext) via <see cref="IServiceScopeFactory"/> on every
/// <see cref="IssueAsync"/> — no captive dependency.
/// </para>
/// </remarks>
public sealed class PoCCsrSigningWorkloadCertificateIssuer : IWorkloadCertificateIssuer
{
    private readonly IServiceScopeFactory r_scopeFactory;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="PoCCsrSigningWorkloadCertificateIssuer"/> class.
    /// </summary>
    /// <param name="scopeFactory">Scope factory for per-call capability resolution.</param>
    public PoCCsrSigningWorkloadCertificateIssuer(IServiceScopeFactory scopeFactory)
    {
        // §5.1a carve-out: plain reference-type null-guard — no present-but-falsey.
        ArgumentNullException.ThrowIfNull(scopeFactory);

        r_scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async ValueTask<D2Result<WorkloadLeafMaterial>> IssueAsync(
        byte[] csrDer, CancellationToken ct = default)
    {
        // CsrVerification accepts null/empty as InvalidCsr — do not ThrowIfNull so the
        // typed 400 path stays consistent with the production issuance surface.
        var verified = CsrVerification.Verify(csrDer);

        if (!verified.Success)
            return D2Result<WorkloadLeafMaterial>.BubbleFail(verified);

        await using var scope = r_scopeFactory.CreateAsyncScope();

        var leafCap = scope.ServiceProvider
            .GetRequiredService<ICaLeafSigningCapability>();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<KeyCustodianOptions>>().Value;

        var workload = WorkloadIdentity.FromTrusted(EdgeHostIdentity.SERVICE_ID);
        var validity = Duration.FromTimeSpan(options.LeafValidity);

        var signed = await leafCap
            .SignLeafAsync(verified.Data!, workload, validity, ct)
            .ConfigureAwait(false);

        if (!signed.Success)
            return D2Result<WorkloadLeafMaterial>.BubbleFail(signed);

        var issued = signed.Data!.Certificate;

        return D2Result<WorkloadLeafMaterial>.Ok(
            new WorkloadLeafMaterial(
                CertificateDer: issued.CertificateDer,
                IssuerCertificateDer: issued.IssuerCertificateDer,
                NotAfter: issued.NotAfter));
    }
}
