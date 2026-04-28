// -----------------------------------------------------------------------
// <copyright file="ITransaction.Rollback.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Repository.Transactions;

using D2.Shared.Handler;

public partial interface ITransaction
{
    /// <summary>
    /// Handler for rolling back a database transaction.
    /// </summary>
    public interface IRollbackHandler : IHandler<RollbackInput, RollbackOutput>;

    /// <summary>
    /// Input for rolling back a database transaction.
    /// </summary>
    public record RollbackInput;

    /// <summary>
    /// Output for rolling back a database transaction.
    /// </summary>
    public record RollbackOutput;
}
