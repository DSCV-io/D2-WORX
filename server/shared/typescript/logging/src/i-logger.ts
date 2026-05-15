// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Per-call structured bindings. Plain object — never an `Error` instance.
 * The `ILogger` API forbids passing `Error` directly because `error.message`
 * leaks broker URI passwords / user input. Use {@link sanitizedErrorRender}
 * to extract a safe shape (`{ name, firstFrame }`) and pass that instead.
 */
export type LogBindings = Readonly<Record<string, unknown>>;

/**
 * Structured logger interface. Mirrors the .NET `D2.Shared.Logging.ILogger`
 * shape: per-level methods accepting `(message, bindings?)` — no
 * `Error`-typed parameter (PII safety; matches .NET's
 * `LoggerMessageDelegateSafetyTests.cs` rule that `[LoggerMessage]` partial
 * methods MUST NOT accept `Exception`).
 */
export interface ILogger {
  trace(message: string, bindings?: LogBindings): void;
  debug(message: string, bindings?: LogBindings): void;
  info(message: string, bindings?: LogBindings): void;
  warn(message: string, bindings?: LogBindings): void;
  error(message: string, bindings?: LogBindings): void;
  fatal(message: string, bindings?: LogBindings): void;

  /** Returns a child logger with the given bindings layered onto every call. */
  child(bindings: LogBindings): ILogger;
}
