// -----------------------------------------------------------------------
// <copyright file="ISystemWorkScopeFactory.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using JetBrains.Annotations;

/// <summary>
/// Platform-owned entry for System-plane work. The only sanctioned way for
/// hosted / background services to open a DI scope that carries
/// <see cref="DcsvIo.D2.Auth.Abstractions.RequestOrigin.System"/> authority.
/// Modules consume this factory; they never register <see cref="IRequestContext"/>
/// themselves.
/// </summary>
public interface ISystemWorkScopeFactory
{
    /// <summary>
    /// Opens a DI scope with
    /// <see cref="DcsvIo.D2.Auth.Abstractions.RequestOrigin.System"/> already
    /// established (host service id as <c>ImmediateCaller</c>, fresh System
    /// call-path entry). Dispose the returned scope when the work unit ends.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An established System work scope.</returns>
    [MustDisposeResource(true)]
    ValueTask<ISystemWorkScope> BeginAsync(CancellationToken cancellationToken = default);
}
