// -----------------------------------------------------------------------
// <copyright file="HandlerOptions.cs" company="DCSV">
// Copyright (c) DCSV. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

namespace D2.Shared.Handler;

/// <summary>
/// Options to customize the behavior of a handler.
/// </summary>
///
/// <param name="SuppressTimeWarnings">
/// Indicates whether to suppress time-related warnings during handling.
/// </param>
/// <param name="LogInput">
/// Indicates whether to log the input provided to the handler.
/// </param>
/// <param name="LogOutput">
/// Indicates whether to log the output produced by the handler.
/// </param>
/// <param name="WarningThresholdMs">
/// The threshold in milliseconds for logging a warning if the handler takes longer than this time.
/// </param>
/// <param name="CriticalThresholdMs">
/// The threshold in milliseconds for logging a critical warning if the handler takes longer than
/// this time.
/// </param>
public record HandlerOptions(
    bool SuppressTimeWarnings = false,
    bool LogInput = true,
    bool LogOutput = true,
    long WarningThresholdMs = 100,
    long CriticalThresholdMs = 500);
