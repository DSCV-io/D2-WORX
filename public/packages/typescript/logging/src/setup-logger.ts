// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { pino, type Logger as PinoLogger } from "pino";

import type { ILogger, LogBindings } from "./i-logger.js";
import { collectAllRedactedFields } from "./redaction.js";

/**
 * Logger options. Defaults: `level=info`, `pretty=false`,
 * `redact={ remove: true }` so redacted paths drop entirely from output.
 *
 * The `redactPaths` array merges with `markRedactedFields()` registrations
 * + codegen-emitted `<TypeName>RedactPaths` constants the caller provides
 * directly. Spec-driven redaction (codegen) + hand-written-type redaction
 * (registry) compose into the single Pino redaction list.
 */
export interface LoggerOptions {
  /** Service name; emitted as `service` on every record. */
  readonly serviceName: string;
  /** Environment label (e.g. `"prod"`, `"local"`). */
  readonly environment?: string;
  /** Minimum level (`trace` / `debug` / `info` / `warn` / `error` / `fatal`). */
  readonly minLevel?: "trace" | "debug" | "info" | "warn" | "error" | "fatal";
  /** Pretty-print transport for local dev. NEVER true in prod. */
  readonly pretty?: boolean;
  /**
   * Additional redact-path arrays — typically codegen-emitted
   * `<TypeName>RedactPaths` constants. These merge with paths registered
   * via `markRedactedFields()` to build Pino's redaction config.
   */
  readonly redactPaths?: readonly (readonly string[])[];
}

/**
 * Pino-backed `ILogger` implementation. Wraps a `PinoLogger` to project
 * the .NET-mirroring per-level shape — `(message, bindings?)`.
 */
class PinoBackedLogger implements ILogger {
  constructor(private readonly inner: PinoLogger) {}

  trace(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.trace(message);
    else this.inner.trace(bindings, message);
  }
  debug(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.debug(message);
    else this.inner.debug(bindings, message);
  }
  info(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.info(message);
    else this.inner.info(bindings, message);
  }
  warn(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.warn(message);
    else this.inner.warn(bindings, message);
  }
  error(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.error(message);
    else this.inner.error(bindings, message);
  }
  fatal(message: string, bindings?: LogBindings): void {
    if (bindings === undefined) this.inner.fatal(message);
    else this.inner.fatal(bindings, message);
  }
  child(bindings: LogBindings): ILogger {
    return new PinoBackedLogger(this.inner.child(bindings));
  }
}

/**
 * Builds an `ILogger` configured for the supplied service. Merges
 * codegen-emitted + hand-written-registered redact paths into the Pino
 * `redact` config so PII never reaches stdout.
 */
export function setupLogger(opts: LoggerOptions): ILogger {
  const paths = new Set<string>();
  for (const arr of opts.redactPaths ?? []) for (const p of arr) paths.add(p);
  for (const p of collectAllRedactedFields()) paths.add(p);

  const inner = pino({
    name: opts.serviceName,
    level: opts.minLevel ?? "info",
    base: {
      service: opts.serviceName,
      environment: opts.environment ?? "unknown",
    },
    redact: paths.size > 0 ? { paths: [...paths], remove: true } : undefined,
    transport: opts.pretty
      ? {
          target: "pino-pretty",
          options: { colorize: true },
        }
      : undefined,
  });
  return new PinoBackedLogger(inner);
}
