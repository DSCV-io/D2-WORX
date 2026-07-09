// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import type { ILogger, LogBindings } from "@d2/logging";
import {
  type IPropagatedContext,
  PropagatedContextSerializer,
} from "@d2/request-context-abstractions";

import { type ConsumedMessage } from "../src/subscribing/consumed-message.js";

/**
 * Encodes a propagated context into the `x-d2-context` wire value —
 * base64url-of-JSON, exactly what the .NET `PropagatedContextSerializer.Encode`
 * and the gRPC context interceptor produce.
 */
export function encodePropagatedHeader(ctx: IPropagatedContext): string {
  return Buffer.from(
    PropagatedContextSerializer.serialize(ctx),
    "utf8",
  ).toString("base64url");
}

/** One captured log call. */
export interface LogEntry {
  readonly level: string;
  readonly message: string;
  readonly bindings?: LogBindings;
}

/** A recording {@link ILogger} for asserting the consumer's observability. */
export class RecordingLogger implements ILogger {
  readonly entries: LogEntry[] = [];

  private record(level: string, message: string, bindings?: LogBindings): void {
    this.entries.push({ level, message, bindings });
  }

  trace(message: string, bindings?: LogBindings): void {
    this.record("trace", message, bindings);
  }

  debug(message: string, bindings?: LogBindings): void {
    this.record("debug", message, bindings);
  }

  info(message: string, bindings?: LogBindings): void {
    this.record("info", message, bindings);
  }

  warn(message: string, bindings?: LogBindings): void {
    this.record("warn", message, bindings);
  }

  error(message: string, bindings?: LogBindings): void {
    this.record("error", message, bindings);
  }

  fatal(message: string, bindings?: LogBindings): void {
    this.record("fatal", message, bindings);
  }

  child(): ILogger {
    return this;
  }

  /** True if any recorded message contains `fragment`. */
  logged(fragment: string): boolean {
    return this.entries.some((e) => e.message.includes(fragment));
  }
}

/** Builds a {@link ConsumedMessage} with sensible defaults for tests. */
export function makeMessage(
  overrides: Partial<ConsumedMessage> = {},
): ConsumedMessage {
  return {
    body: Buffer.from(JSON.stringify({ hello: "world" }), "utf8"),
    messageId: "0192f8c1-1111-7000-8000-000000000001",
    headers: {},
    deliveryTag: 1,
    redelivered: false,
    exchange: "d2.security.key-rotated",
    routingKey: "",
    ...overrides,
  };
}

const SAMPLE_TRACE_ID = "4bf92f3577b34da6a3ce929d0e0e4736";
const SAMPLE_SPAN_ID = "00f067aa0ba902b7";

/** A valid W3C traceparent carrying a known trace id (for linkage assertions). */
export const SAMPLE_TRACEPARENT = `00-${SAMPLE_TRACE_ID}-${SAMPLE_SPAN_ID}-01`;
export const SAMPLE_PRODUCER_TRACE_ID = SAMPLE_TRACE_ID;
