// -----------------------------------------------------------------------
// <copyright file="PostgresServiceCollectionExtensions.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Handler.Repo.Postgres;

using DcsvIo.D2.Handler.Repo.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI registration helpers for the PostgreSQL repo-handler integration.
/// </summary>
public static class PostgresServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="PostgresDbExceptionClassifier"/> as the
        /// singleton <see cref="IDbExceptionClassifier"/> implementation.
        /// Composition roots that consume <c>BaseRepoHandler</c> against
        /// PostgreSQL must call this; the base handler fails at resolution
        /// time without a registered classifier.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uses <c>TryAddSingleton&lt;TService, TImplementation&gt;</c>
        /// — calling multiple times is safe (no duplicate registration). A
        /// custom <see cref="IDbExceptionClassifier"/> registered BEFORE this
        /// call wins (TryAdd sees an existing registration and is a no-op):
        /// <code>
        /// services.AddSingleton&lt;IDbExceptionClassifier, MyCustomClassifier&gt;();
        /// services.AddD2Postgres();   // no-op
        /// </code>
        /// </para>
        /// <para>
        /// <b>Don't register multiple classifiers via plain
        /// <c>AddSingleton&lt;IDbExceptionClassifier, X&gt;()</c></b> — DI's
        /// last-registered-wins resolution leaves any earlier classifier as an
        /// orphaned singleton in the graph (constructed if anyone enumerates
        /// <c>IEnumerable&lt;IDbExceptionClassifier&gt;</c>) and the resolved
        /// instance depends on call order, which is brittle. If a service
        /// genuinely needs more than one classifier (e.g. one connection to
        /// Postgres, one to an embedded SQLite for tests), use keyed
        /// registration + keyed resolution:
        /// <code>
        /// services.AddKeyedSingleton&lt;IDbExceptionClassifier,
        ///     PostgresDbExceptionClassifier&gt;("primary");
        /// services.AddKeyedSingleton&lt;IDbExceptionClassifier,
        ///     SqliteDbExceptionClassifier&gt;("scratch");
        /// // Inject via [FromKeyedServices("primary")] IDbExceptionClassifier classifier
        /// </code>
        /// (Keyed services require .NET 8+ — the codebase targets .NET 10, so
        /// the API is available.)
        /// </para>
        /// </remarks>
        /// <returns>The service collection (for chaining).</returns>
        public IServiceCollection AddD2Postgres()
        {
            services.TryAddSingleton<IDbExceptionClassifier, PostgresDbExceptionClassifier>();
            return services;
        }
    }
}
