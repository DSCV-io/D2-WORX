// -----------------------------------------------------------------------
// <copyright file="Rollback.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Transactions.Pg.Transactions;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using H = D2.Shared.Interfaces.Repository.Transactions.ITransaction.IRollbackHandler;
using I = D2.Shared.Interfaces.Repository.Transactions.ITransaction.RollbackInput;
using O = D2.Shared.Interfaces.Repository.Transactions.ITransaction.RollbackOutput;

/// <summary>
/// Handler for rolling back a database transaction.
/// </summary>
public class Rollback : BaseHandler<Rollback, I, O>, H
{
    private readonly DbContext r_db;

    /// <summary>
    /// Initializes a new instance of the <see cref="Rollback"/> class.
    /// </summary>
    ///
    /// <param name="db">
    /// The database context.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Rollback(
        DbContext db,
        IHandlerContext context)
        : base(context)
    {
        r_db = db;
    }

    /// <summary>
    /// Executes the handler to roll back a database transaction.
    /// </summary>
    ///
    /// <param name="input">
    /// The input parameters for the handler.
    /// </param>
    /// <param name="ct">
    /// The cancellation token.
    /// </param>
    ///
    /// <returns>
    /// A task that represents the asynchronous operation, containing the result of the handler
    /// execution.
    /// </returns>
    protected override async ValueTask<D2Result<O?>> ExecuteAsync(
        I input,
        CancellationToken ct = default)
    {
        await r_db.Database.RollbackTransactionAsync(ct);

        return D2Result<O?>.Ok();
    }
}
