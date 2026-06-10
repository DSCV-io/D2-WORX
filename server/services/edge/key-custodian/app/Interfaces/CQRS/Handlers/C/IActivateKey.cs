// -----------------------------------------------------------------------
// <copyright file="IActivateKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Smoke-tests and activates a pending key once its soak window has elapsed.
/// Used for bootstrap and post-compromise activation (routine rotation uses
/// <c>RotateKey</c>'s atomic swap).
/// </summary>
public interface IActivateKey : IHandler<ActivateKeyInput, KeySummary>;
