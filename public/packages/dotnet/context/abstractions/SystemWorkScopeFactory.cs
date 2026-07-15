// -----------------------------------------------------------------------
// <copyright file="SystemWorkScopeFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using DcsvIo.D2.Auth.Abstractions;
using DcsvIo.D2.Time;
using DcsvIo.D2.Utilities.Extensions;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

/// <summary>
/// Default <see cref="ISystemWorkScopeFactory"/>: creates a DI scope, always
/// calls <see cref="SystemRequestContextBootstrap.EstablishSystemContext"/>, and
/// returns a disposable <see cref="ISystemWorkScope"/>.
/// </summary>
internal sealed class SystemWorkScopeFactory(
    IServiceScopeFactory scopeFactory,
    IOptions<D2WorkloadIdentityOptions> workloadIdentity,
    IClock clock)
    : ISystemWorkScopeFactory
{
    private readonly IServiceScopeFactory r_scopeFactory = scopeFactory;
    private readonly IClock r_clock = clock;
    private readonly string r_hostServiceId = workloadIdentity.Value.ServiceId;

    /// <inheritdoc/>
    [MustDisposeResource(true)]
    public async ValueTask<ISystemWorkScope> BeginAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        r_hostServiceId.ThrowIfFalsey();

        var scope = r_scopeFactory.CreateAsyncScope();

        try
        {
            scope.ServiceProvider.EstablishSystemContext(r_hostServiceId, r_clock);
            return new SystemWorkScope(scope);
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }
}
