// -----------------------------------------------------------------------
// <copyright file="IGenerateKey.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Interfaces.CQRS.Handlers.C;

using D2.Edge.KeyCustodian.App.Models;
using D2.Shared.Handler.Abstractions;

/// <summary>
/// Generates a new pending key for a domain: generates fresh material, root-wraps
/// it, mints a kid, and persists the pending row + a <c>Generated</c> audit entry.
/// </summary>
public interface IGenerateKey : IHandler<GenerateKeyInput, KeySummary>;
