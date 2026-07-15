// -----------------------------------------------------------------------
// <copyright file="SystemWorkScope.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Default <see cref="ISystemWorkScope"/> — thin wrapper over
/// <see cref="AsyncServiceScope"/> so System workers dispose the same way as
/// any other async DI scope.
/// </summary>
[MustDisposeResource]
internal sealed class SystemWorkScope(AsyncServiceScope scope) : ISystemWorkScope
{
    /// <inheritdoc/>
    public IServiceProvider Services => scope.ServiceProvider;

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => scope.DisposeAsync();
}
