// -----------------------------------------------------------------------
// <copyright file="CompromiseKeyOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.CompromiseKey;

/// <summary>
/// Result of compromising a key: the kid marked compromised and the optional
/// replacement pending key's kid (when a replacement was requested).
/// </summary>
/// <param name="CompromisedKid">The kid that was marked compromised.</param>
/// <param name="ReplacementKid">
/// The kid of the auto-generated replacement pending key; <see langword="null"/>
/// when no replacement was requested.
/// </param>
public sealed record CompromiseKeyOutput(string CompromisedKid, string? ReplacementKid);
