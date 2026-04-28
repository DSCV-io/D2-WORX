// -----------------------------------------------------------------------
// <copyright file="Begin.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Transactions.Pg.Transactions;

using D2.Shared.Handler;
using D2.Shared.Result;
using Microsoft.EntityFrameworkCore;
using E = D2.Shared.Utilities.Enums.IsolationLevel;
using H = D2.Shared.Interfaces.Repository.Transactions.ITransaction.IBeginHandler;
using I = D2.Shared.Interfaces.Repository.Transactions.ITransaction.BeginInput;
using O = D2.Shared.Interfaces.Repository.Transactions.ITransaction.BeginOutput;
using S = System.Data.IsolationLevel;

/// <summary>
/// Handler for beginning a database transaction.
/// </summary>
public class Begin : BaseHandler<Begin, I, O>, H
{
    private readonly DbContext r_db;

    /// <summary>
    /// Initializes a new instance of the <see cref="Begin"/> class.
    /// </summary>
    ///
    /// <param name="db">
    /// The database context.
    /// </param>
    /// <param name="context">
    /// The handler context.
    /// </param>
    public Begin(
        DbContext db,
        IHandlerContext context)
        : base(context)
    {
        r_db = db;
    }

    /// <summary>
    /// Executes the handler to begin a database transaction.
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
        var isolationLevel = input.Level switch
        {
            E.ReadUncommitted => S.ReadUncommitted,
            E.ReadCommitted => S.ReadCommitted,
            E.RepeatableRead => S.RepeatableRead,
            E.Serializable => S.Serializable,
            _ => S.ReadCommitted,
        };

        await r_db.Database.BeginTransactionAsync(isolationLevel, ct);

        return D2Result<O?>.Ok();
    }
}
