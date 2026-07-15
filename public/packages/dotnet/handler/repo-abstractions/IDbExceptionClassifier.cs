// -----------------------------------------------------------------------
// <copyright file="IDbExceptionClassifier.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Abstractions;

using System;

/// <summary>
/// Provider-specific classifier for database exceptions. The base repo
/// handler uses an injected implementation to translate any
/// <see cref="Exception"/> thrown during a DB operation into a
/// provider-agnostic <see cref="DbFailureKind"/>.
/// </summary>
/// <remarks>
/// One implementation per database provider. PostgreSQL lives in
/// <c>DcsvIo.D2.Handler.Repo.Postgres</c>; future SQL Server / SQLite /
/// MySQL implementations would be sibling packages registering the same
/// interface. The base repo handler stays provider-agnostic.
/// <para>
/// Implementations MUST be thread-safe. The classifier is registered as a
/// DI singleton and concurrently invoked from request threads via
/// <c>BaseRepoHandler</c>. Stateless designs (static helpers, no instance
/// fields) trivially satisfy the contract; if state is added, lock or use
/// <see cref="System.Collections.Concurrent"/> primitives.
/// </para>
/// </remarks>
public interface IDbExceptionClassifier
{
    /// <summary>
    /// Inspects <paramref name="exception"/> and returns the matching
    /// <see cref="DbFailureKind"/>, or <c>null</c> if the exception is not
    /// a recognized DB failure (in which case the caller propagates it as an
    /// unhandled exception).
    /// </summary>
    /// <param name="exception">The exception thrown during a DB operation.</param>
    /// <returns>The classified failure kind, or <c>null</c>.</returns>
    DbFailureKind? Classify(Exception exception);
}
