// -----------------------------------------------------------------------
// <copyright file="ICompromiseKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;

/// <summary>
/// Marks a live key compromised and (by default) auto-generates a replacement
/// pending key for its domain, then announces the compromise urgently (carrying
/// the session-invalidation signal).
/// </summary>
public interface ICompromiseKeyHandler : IHandler<CompromiseKeyInput, CompromiseKeyOutput>;
