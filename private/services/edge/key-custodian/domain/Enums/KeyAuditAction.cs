// -----------------------------------------------------------------------
// <copyright file="KeyAuditAction.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.Domain.Enums;

/// <summary>
/// Discriminates the lifecycle transition that produced an
/// <c>EncryptionKeyAudit</c> entry.
/// </summary>
public enum KeyAuditAction
{
    /// <summary>A new key was generated and placed in the <c>Pending</c> state.</summary>
    Generated,

    /// <summary>A pending key was smoke-tested and activated.</summary>
    Activated,

    /// <summary>An active key was rotated; it entered the <c>Retiring</c> state.</summary>
    Rotated,

    /// <summary>
    /// A retiring key's grace window elapsed; it entered the <c>Retired</c> state.
    /// </summary>
    Retired,

    /// <summary>A live key was marked compromised and immediately removed from service.</summary>
    Compromised,
}
