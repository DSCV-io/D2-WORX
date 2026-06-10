// -----------------------------------------------------------------------
// <copyright file="RetireKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

/// <summary>
/// Input to <c>RetireKey</c>: the kid of the retiring key whose grace window has
/// elapsed.
/// </summary>
/// <param name="Kid">The retiring key's identifier.</param>
public sealed record RetireKeyInput(string? Kid);
