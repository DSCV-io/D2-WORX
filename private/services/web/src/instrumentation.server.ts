// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { IncomingMessage } from "node:http";
import { loadEnv } from "@d2/service-defaults/config";
import { NodeSDK } from "@opentelemetry/sdk-node";
import { OTLPTraceExporter } from "@opentelemetry/exporter-trace-otlp-http";
import { OTLPLogExporter } from "@opentelemetry/exporter-logs-otlp-http";
import { OTLPMetricExporter } from "@opentelemetry/exporter-metrics-otlp-http";
import { PeriodicExportingMetricReader } from "@opentelemetry/sdk-metrics";
import { LoggerProvider, BatchLogRecordProcessor } from "@opentelemetry/sdk-logs";
import { getNodeAutoInstrumentations } from "@opentelemetry/auto-instrumentations-node";
import { resourceFromAttributes } from "@opentelemetry/resources";
import { ATTR_SERVICE_NAME } from "@opentelemetry/semantic-conventions";
import {
  CompositePropagator,
  W3CTraceContextPropagator,
  W3CBaggagePropagator,
} from "@opentelemetry/core";
import type { Span } from "@opentelemetry/api";
import { createAddHookMessageChannel } from "import-in-the-middle";
import { register } from "node:module";

// Load .env.local BEFORE anything else — mirrors Node.js services which use
// `--import @d2/service-defaults/register`. SvelteKit can't use --import
// (Vite manages the process), so we call loadEnv() here instead.
loadEnv();

/**
 * Property key used to store the OTel HTTP server span on Node.js IncomingMessage.
 *
 * Vite's dev server (and SvelteKit's production adapter) break the OTel async
 * context chain, so `trace.getActiveSpan()` returns undefined inside SvelteKit
 * hooks. Same problem as `@hono/node-server`.
 *
 * The HTTP instrumentation's `requestHook` stashes the span here, and
 * `getServerSpan()` retrieves it from the raw request.
 */
const OTEL_SPAN_KEY = "__otelSpan" as const;

interface OTelIncomingMessage extends IncomingMessage {
  [OTEL_SPAN_KEY]?: Span;
}

/**
 * Map from Node.js IncomingMessage → OTel server span.
 *
 * SvelteKit hooks receive a Web `Request` (not `IncomingMessage`), so we can't
 * read `__otelSpan` directly. Instead, the `requestHook` stashes the span on
 * the IncomingMessage via `__otelSpan`, and we use a WeakMap keyed by
 * IncomingMessage to bridge to Web Request lookups.
 *
 * Uses WeakMap so entries are GC'd when IncomingMessage is collected — no
 * manual cleanup or TTL needed.
 */
const spanByIncoming = new WeakMap<IncomingMessage, Span>();

/**
 * Maps a Web Request URL to its IncomingMessage for span lookup.
 * Keyed by `METHOD:pathname:requestId` where requestId is a per-request
 * counter to avoid collisions from concurrent requests to the same path.
 */
const incomingByKey = new Map<string, { incoming: IncomingMessage; timestamp: number }>();
const KEY_MAP_TTL = 30_000;
let requestCounter = 0;

function cleanupKeyMap(): void {
  const now = Date.now();
  for (const [key, entry] of incomingByKey) {
    if (now - entry.timestamp > KEY_MAP_TTL) incomingByKey.delete(key);
  }
}

// Periodic cleanup every 60s for the key→incoming bridge map
setInterval(cleanupKeyMap, 60_000).unref();

/**
 * Builds a unique key for an IncomingMessage request.
 * Appends a monotonic counter to METHOD:pathname to avoid collisions
 * when multiple concurrent requests hit the same route.
 */
function makeRequestKey(method: string, pathname: string): string {
  return `${method}:${pathname}:${++requestCounter}`;
}

/**
 * Retrieves the OTel server span for a SvelteKit request event.
 * Call from SvelteKit hooks where `trace.getActiveSpan()` returns undefined.
 *
 * Looks up the span via the IncomingMessage stored in the bridge map.
 * Does NOT delete the entry — multiple hooks may need the span within
 * the same request lifecycle. Cleanup happens via TTL.
 */
export function getServerSpan(request: Request): Span | undefined {
  const pathname = new URL(request.url).pathname;
  const prefix = `${request.method}:${pathname}:`;

  // Find the oldest matching entry for this METHOD:pathname (FIFO).
  // SvelteKit processes requests in arrival order, so the oldest unmatched
  // entry corresponds to the current Web Request being handled.
  let bestKey: string | undefined;
  let bestCounter = Infinity;
  for (const key of incomingByKey.keys()) {
    if (key.startsWith(prefix)) {
      const counter = parseInt(key.slice(prefix.length), 10);
      if (counter < bestCounter) {
        bestCounter = counter;
        bestKey = key;
      }
    }
  }

  if (bestKey) {
    const entry = incomingByKey.get(bestKey);
    if (entry) {
      incomingByKey.delete(bestKey);
      return spanByIncoming.get(entry.incoming);
    }
  }
  return undefined;
}

const serviceName = process.env.OTEL_SERVICE_NAME ?? "d2-sveltekit";

const tracesEndpoint = process.env.OTEL_EXPORTER_OTLP_TRACES_ENDPOINT;
const logsEndpoint = process.env.OTEL_EXPORTER_OTLP_LOGS_ENDPOINT;
const metricsEndpoint = process.env.OTEL_EXPORTER_OTLP_METRICS_ENDPOINT;

if (!tracesEndpoint || !logsEndpoint || !metricsEndpoint) {
  console.error(
    "[OTel] FATAL: Missing OTEL_EXPORTER_OTLP_*_ENDPOINT env vars. " +
      "Telemetry will not be exported. Set OTEL_EXPORTER_OTLP_TRACES_ENDPOINT, " +
      "OTEL_EXPORTER_OTLP_LOGS_ENDPOINT, and OTEL_EXPORTER_OTLP_METRICS_ENDPOINT.",
  );
}

const traceExporter = new OTLPTraceExporter({ url: tracesEndpoint });
const logExporter = new OTLPLogExporter({ url: logsEndpoint });
const metricExporter = new OTLPMetricExporter({ url: metricsEndpoint });

// Set up logging
const loggerProvider = new LoggerProvider({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: serviceName,
    service_name: serviceName,
    service: serviceName,
  }),
  processors: [new BatchLogRecordProcessor(logExporter)],
});

const { registerOptions } = createAddHookMessageChannel();
register("import-in-the-middle/hook.mjs", import.meta.url, registerOptions);

export const sdk = new NodeSDK({
  resource: resourceFromAttributes({
    [ATTR_SERVICE_NAME]: serviceName,
    service_name: serviceName,
    service: serviceName,
  }),
  traceExporter,
  logRecordProcessor: new BatchLogRecordProcessor(logExporter),
  metricReader: new PeriodicExportingMetricReader({
    exporter: metricExporter,
    exportIntervalMillis: 15000, // Export every 15 seconds
  }),
  // Propagate both W3C trace context AND baggage headers on outgoing HTTP calls.
  // Baggage carries identity/network context (userId, deviceFingerprint, etc.)
  // set by the auth hook — downstream services receive it automatically.
  textMapPropagator: new CompositePropagator({
    propagators: [new W3CTraceContextPropagator(), new W3CBaggagePropagator()],
  }),
  instrumentations: [
    getNodeAutoInstrumentations({
      "@opentelemetry/instrumentation-fs": {
        enabled: false,
      },
      "@opentelemetry/instrumentation-http": {
        requestHook: (span: Span, request: unknown) => {
          if (request instanceof IncomingMessage) {
            // Stash span on raw request (same pattern as @d2/service-defaults)
            (request as OTelIncomingMessage)[OTEL_SPAN_KEY] = span;
            // Store span on IncomingMessage (WeakMap — GC'd automatically)
            spanByIncoming.set(request, span);
            // Bridge: key → IncomingMessage so getServerSpan(Web Request) can find it.
            // Uses a unique counter suffix to avoid collisions from concurrent
            // requests to the same route (e.g., two GET:/dashboard at once).
            const rawUrl = request.url ?? "/";
            const pathname = rawUrl.split("?")[0];
            const key = makeRequestKey(request.method ?? "GET", pathname);
            incomingByKey.set(key, { incoming: request, timestamp: Date.now() });
          }
        },
        ignoreIncomingRequestHook: (req) => {
          const url = req.url || "";
          return (
            url.includes("?v=") ||
            url.includes("/@") ||
            url.startsWith("/node_modules") ||
            /\.(js|ts|svelte|css|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot)(\?|$)/.test(url) ||
            url === "/favicon.ico" ||
            url === "/health" ||
            url.endsWith(".map") ||
            url.startsWith("/.well-known/")
          );
        },
      },
    }),
  ],
});

sdk.start();

export { loggerProvider };
