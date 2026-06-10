// -----------------------------------------------------------------------
// <copyright file="RotateKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Models;

/// <summary>
/// Input to <c>RotateKey</c>: the domain whose active incumbent should be
/// rotated to its soaked pending successor.
/// </summary>
/// <param name="Domain">The target key domain.</param>
public sealed record RotateKeyInput(string? Domain);
