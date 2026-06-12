// -----------------------------------------------------------------------
// <copyright file="GenerateKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.GenerateKey;

/// <summary>
/// Input to <c>GenerateKey</c>: which domain + key type to generate a new
/// pending key for.
/// </summary>
/// <param name="Domain">The target key domain (validated against the catalog).</param>
/// <param name="KeyType">The cryptographic algorithm category to generate.</param>
public sealed record GenerateKeyInput(string? Domain, KeyType KeyType);
