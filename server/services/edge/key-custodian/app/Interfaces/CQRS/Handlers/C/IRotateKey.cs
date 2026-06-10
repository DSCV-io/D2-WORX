// -----------------------------------------------------------------------
// <copyright file="IRotateKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Atomically rotates a domain's active incumbent to its soaked pending
/// successor: marks the incumbent retiring and activates the successor in one
/// transaction, then announces the rotation.
/// </summary>
public interface IRotateKey : IHandler<RotateKeyInput, RotationOutcome>;
