// -----------------------------------------------------------------------
// <copyright file="IRetireKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RetireKey;

using D2.Edge.KeyCustodian.Domain.Rules;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Retires a retiring key once its grace window has elapsed, moving it to the
/// terminal retired state.
/// </summary>
public interface IRetireKeyHandler : IHandler<RetireKeyInput, KeySummary>;
