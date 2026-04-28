// -----------------------------------------------------------------------
// <copyright file="Commit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Transactions.Pg.Transactions;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using H = D2.Shared.Interfaces.Repository.Transactions.ITransaction.ICommitHandler;
using I = D2.Shared.Interfaces.Repository.Transactions.ITransaction.CommitInput;
using O = D2.Shared.Interfaces.Repository.Transactions.ITransaction.CommitOutput;

/// <summary>
/// Handler for committing a database transaction.
/// </summary>
public class Commit : BaseHandler<Commit, I, O>, H
{
    private readonly DbContext r_db;

    /// <summary>
    /// Initializes a new instance of the <see cref="Commit"/> class.
    /// </summary>
    ///
    /// <param name="db">
    /// The database context.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Commit(
        DbContext db,
        IHandlerContext context)
        : base(context)
    {
        r_db = db;
    }

    /// <summary>
    /// Executes the handler to commit a database transaction.
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
        await r_db.Database.CommitTransactionAsync(ct);

        return D2Result<O?>.Ok();
    }
}
