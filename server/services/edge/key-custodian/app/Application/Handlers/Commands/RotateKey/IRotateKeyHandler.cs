// -----------------------------------------------------------------------
// <copyright file="IRotateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;

/// <summary>
/// Atomically rotates a domain's active incumbent to its soaked pending
/// successor: marks the incumbent retiring and activates the successor in one
/// transaction, then announces the rotation.
/// </summary>
public interface IRotateKeyHandler : IHandler<RotateKeyInput, RotateKeyOutput>;
