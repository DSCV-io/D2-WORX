// -----------------------------------------------------------------------
// <copyright file="IRetireKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Retires a retiring key once its grace window has elapsed, moving it to the
/// terminal retired state.
/// </summary>
public interface IRetireKey : IHandler<RetireKeyInput, KeySummary>;
