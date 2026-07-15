// -----------------------------------------------------------------------
// <copyright file="RotateKeyInput.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace DcsvIo.D2.Private.Edge.KeyCustodian.App.Application.Handlers.Commands.RotateKey;

/// <summary>
/// Input to <c>RotateKey</c>: the domain whose active incumbent should be
/// rotated to its soaked pending successor.
/// </summary>
/// <param name="Domain">The target key domain.</param>
public sealed record RotateKeyInput(string? Domain);
