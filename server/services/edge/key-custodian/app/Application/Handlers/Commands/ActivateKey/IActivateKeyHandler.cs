// -----------------------------------------------------------------------
// <copyright file="IActivateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.ActivateKey;

/// <summary>
/// Smoke-tests and activates a pending key once its soak window has elapsed.
/// Used for bootstrap and post-compromise activation (routine rotation uses
/// <c>RotateKey</c>'s atomic swap).
/// </summary>
public interface IActivateKeyHandler : IHandler<ActivateKeyInput, KeySummary>;
