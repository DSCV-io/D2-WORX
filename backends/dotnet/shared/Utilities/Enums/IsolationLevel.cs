// -----------------------------------------------------------------------
// <copyright file="IsolationLevel.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Utilities.Enums;

/// <summary>
/// Specifies the isolation level for database transactions.
/// </summary>
public enum IsolationLevel
{
    // Table of isolation levels and their phenomena:
    //
    // Isolation level  | Dirty Reads  | Non-Repeatable Reads | Phantom Reads  | Serialization Anomaly
    // -----------------|--------------|----------------------|----------------|-----------------------
    // Read Uncommitted | Yes          | Yes                  | Yes            | Yes
    // Read Committed   | No           | Yes                  | Yes            | Yes
    // Repeatable Read  | No           | No                   | Yes            | Yes
    // Serializable     | No           | No                   | No             | No

    /// <summary>
    /// The default isolation level of the database. Sets and reads data in its own snapshot.
    /// </summary>
    ReadCommitted,

    /// <summary>
    /// Allows reading uncommitted changes from other transactions.
    /// </summary>
    /// <remarks>
    /// In PostgreSQL, this isolation level behaves the same as ReadCommitted.
    /// </remarks>
    ReadUncommitted,

    /// <summary>
    /// Ensures that any data read during the transaction cannot be changed by other transactions
    /// until the current transaction is complete.
    /// </summary>
    /// <remarks>
    /// Phantom reads can still occur in this isolation level but not in PostgreSQL.
    /// </remarks>
    RepeatableRead,

    /// <summary>
    /// The highest isolation level, which prevents other transactions from reading or writing
    /// data that is being used in the current transaction until it is complete.
    /// </summary>
    Serializable,
}
