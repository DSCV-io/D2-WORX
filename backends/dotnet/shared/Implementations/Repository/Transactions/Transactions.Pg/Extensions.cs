// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Transactions.Pg;

using D2.Shared.Interfaces.Repository.Transactions;
using D2.Shared.Transactions.Pg.Transactions;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering PostgreSQL transaction handlers.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds PostgreSQL transaction handlers to the service collection.
    /// </summary>
    ///
    /// <param name="services">
    /// The service collection to add the handlers to.
    /// </param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers PostgreSQL transaction handlers.
        /// </summary>
        ///
        /// <returns>
        /// The updated service collection.
        /// </returns>
        public IServiceCollection AddPgTransactions()
        {
            services.AddTransient<ITransaction.IBeginHandler, Begin>();
            services.AddTransient<ITransaction.ICommitHandler, Commit>();
            services.AddTransient<ITransaction.IRollbackHandler, Rollback>();

            return services;
        }
    }
}
