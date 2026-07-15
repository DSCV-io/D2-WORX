// -----------------------------------------------------------------------
// <copyright file="IsolationLevel.cs" company="DCSV">
// Copyright (c) DCSV. Licensed under the Apache License, Version 2.0.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Utilities.Enums;

/// <summary>
/// Specifies the isolation level for database transactions.
/// </summary>
/// <remarks>
/// <para>
/// Phenomena matrix:
/// </para>
/// <list type="table">
///   <listheader>
///     <term>Isolation level</term>
///     <description>Dirty / Non-Repeatable / Phantom / Serialization Anomaly</description>
///   </listheader>
///   <item>
///     <term>Read Uncommitted</term>
///     <description>Yes / Yes / Yes / Yes</description>
///   </item>
///   <item>
///     <term>Read Committed</term>
///     <description>No / Yes / Yes / Yes</description>
///   </item>
///   <item>
///     <term>Repeatable Read</term>
///     <description>No / No / Yes / Yes</description>
///   </item>
///   <item>
///     <term>Serializable</term>
///     <description>No / No / No / No</description>
///   </item>
/// </list>
/// </remarks>
public enum IsolationLevel
{
    /// <summary>
    /// The default isolation level of the database. Sets and reads data in its
    /// own snapshot.
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
    /// Ensures that any data read during the transaction cannot be changed by
    /// other transactions until the current transaction is complete.
    /// </summary>
    /// <remarks>
    /// Phantom reads can still occur in this isolation level but not in PostgreSQL.
    /// </remarks>
    RepeatableRead,

    /// <summary>
    /// The highest isolation level, which prevents other transactions from
    /// reading or writing data that is being used in the current transaction
    /// until it is complete.
    /// </summary>
    Serializable,
}
