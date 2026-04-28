// -----------------------------------------------------------------------
// <copyright file="ITransaction.Commit.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Interfaces.Repository.Transactions;

using D2.Shared.Handler;

public partial interface ITransaction
{
    /// <summary>
    /// Handler for committing a database transaction.
    /// </summary>
    public interface ICommitHandler : IHandler<CommitInput, CommitOutput>;

    /// <summary>
    /// Input for committing a database transaction.
    /// </summary>
    public record CommitInput;

    /// <summary>
    /// Output for committing a database transaction.
    /// </summary>
    public record CommitOutput;
}
