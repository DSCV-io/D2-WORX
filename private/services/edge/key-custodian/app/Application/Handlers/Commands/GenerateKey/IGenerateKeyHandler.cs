// -----------------------------------------------------------------------
// <copyright file="IGenerateKeyHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;

/// <summary>
/// Generates a new pending key for a domain: generates fresh material, root-wraps
/// it, mints a kid, and persists the pending row + a <c>Generated</c> audit entry.
/// </summary>
public interface IGenerateKeyHandler : IHandler<GenerateKeyInput, KeySummary>;
