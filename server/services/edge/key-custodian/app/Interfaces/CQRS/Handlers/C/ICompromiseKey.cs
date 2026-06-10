// -----------------------------------------------------------------------
// <copyright file="ICompromiseKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Marks a live key compromised and (by default) auto-generates a replacement
/// pending key for its domain, then announces the compromise urgently (carrying
/// the session-invalidation signal).
/// </summary>
public interface ICompromiseKey : IHandler<CompromiseKeyInput, CompromiseOutcome>;
