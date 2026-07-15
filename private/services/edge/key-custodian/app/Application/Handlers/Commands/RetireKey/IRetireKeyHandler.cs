// -----------------------------------------------------------------------
// <copyright file="IRetireKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;

/// <summary>
/// Retires a retiring key once its grace window has elapsed, moving it to the
/// terminal retired state.
/// </summary>
public interface IRetireKeyHandler : IHandler<RetireKeyInput, KeySummary>;
