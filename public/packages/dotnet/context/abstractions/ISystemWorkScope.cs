// -----------------------------------------------------------------------
// <copyright file="ISystemWorkScope.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Context.Abstractions;

/// <summary>
/// A disposable DI scope opened by <see cref="ISystemWorkScopeFactory"/> for
/// hosted / background authority-bearing work.
/// <see cref="DcsvIo.D2.Auth.Abstractions.RequestOrigin.System"/> is already
/// established on the scope's request context when the factory returns.
/// </summary>
public interface ISystemWorkScope : IAsyncDisposable
{
    /// <summary>
    /// Gets the scoped <see cref="IServiceProvider"/> for this System work unit.
    /// Resolve handlers and other scoped dependencies from here — never from the
    /// root provider, and never via a hand-rolled <c>CreateAsyncScope</c> that
    /// bypasses <see cref="ISystemWorkScopeFactory"/>.
    /// </summary>
    IServiceProvider Services { get; }
}
