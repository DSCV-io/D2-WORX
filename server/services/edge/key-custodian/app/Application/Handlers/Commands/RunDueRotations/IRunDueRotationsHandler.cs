// -----------------------------------------------------------------------
// <copyright file="IRunDueRotationsHandler.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Edge.KeyCustodian.App.Application.Handlers.Commands.RunDueRotations;

/// <summary>
/// Executes all key-lifecycle actions that are currently due across all domains:
/// bootstrap, activate, rotate, generate-successor, and retire.
/// </summary>
/// <remarks>
/// Designed for use by a scheduler job. Per-domain sub-handler failures are
/// tolerated and counted in <see cref="RunDueRotationsOutput.Errors"/> rather
/// than aborting the entire run.
/// </remarks>
public interface IRunDueRotationsHandler : IHandler<RunDueRotationsInput, RunDueRotationsOutput>;
