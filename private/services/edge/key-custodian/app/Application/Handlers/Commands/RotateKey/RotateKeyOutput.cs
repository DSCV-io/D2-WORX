// -----------------------------------------------------------------------
// <copyright file="RotateKeyOutput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;

/// <summary>
/// Result of an atomic rotation: the incumbent that entered the retiring state
/// and the successor that was activated.
/// </summary>
/// <param name="RetiringKid">The kid that is now retiring.</param>
/// <param name="ActivatedKid">The kid that is now active.</param>
public sealed record RotateKeyOutput(string RetiringKid, string ActivatedKid);
