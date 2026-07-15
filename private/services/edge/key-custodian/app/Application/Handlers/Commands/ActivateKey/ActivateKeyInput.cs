// -----------------------------------------------------------------------
// <copyright file="ActivateKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;

/// <summary>
/// Input to <c>ActivateKey</c>: the kid of the pending key to smoke-test and
/// activate.
/// </summary>
/// <param name="Kid">The pending key's identifier.</param>
public sealed record ActivateKeyInput(string? Kid);
