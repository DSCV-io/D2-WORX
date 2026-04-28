// -----------------------------------------------------------------------
// <copyright file="ITransaction.Begin.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Repository.Transactions;

using D2.Shared.Handler;
using D2.Shared.Utilities.Enums;

public partial interface ITransaction
{
    /// <summary>
    /// Handler for beginning a database transaction.
    /// </summary>
    public interface IBeginHandler : IHandler<BeginInput, BeginOutput>;

    /// <summary>
    /// Input for beginning a database transaction.
    /// </summary>
    /// <param name="Level">
    /// The isolation level for the transaction. Defaults to ReadCommitted.
    /// </param>
    public record BeginInput(IsolationLevel Level = IsolationLevel.ReadCommitted);

    /// <summary>
    /// Output for beginning a database transaction.
    /// </summary>
    public record BeginOutput;
}
