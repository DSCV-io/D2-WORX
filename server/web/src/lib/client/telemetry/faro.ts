import { type Faro, getWebInstrumentations, initializeFaro, LogLevel } from "@grafana/faro-web-sdk";
import { TracingInstrumentation } from "@grafana/faro-web-tracing";
import { env } from "$env/dynamic/public";
import { dev } from "$app/environment";

let faro: Faro | undefined;

/**
 * Initialize Grafana Faro for client-side telemetry.
 * Safe to call from SSR — returns early if `window` is unavailable.
 * Gracefully degrades if the collector is unreachable.
 */
export function initFaro(): Faro | undefined {
  if (typeof window === "undefined") return undefined;
  if (faro) return faro;

  const collectorUrl = env.PUBLIC_FARO_COLLECTOR_URL ?? "http://localhost:12347/collect";
  const gatewayUrl = env.PUBLIC_GATEWAY_URL ?? "http://localhost:5461";

  try {
    faro = initializeFaro({
      url: collectorUrl,
      app: {
        name: "d2-sveltekit-frontend",
        version: "0.0.1",
        environment: dev ? "development" : "production",
      },

      instrumentations: [
        ...getWebInstrumentations({
          captureConsole: true,
          captureConsoleDisabledLevels: [LogLevel.DEBUG, LogLevel.TRACE],
        }),
        new TracingInstrumentation({
          instrumentationOptions: {
            propagateTraceHeaderCorsUrls: [
              new RegExp(gatewayUrl.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")),
            ],
          },
        }),
      ],

      sessionTracking: {
        enabled: true,
      },

      beforeSend: (event) => {
        // Filter out SvelteKit internal data fetches from logs
        if (event.meta?.page?.url && /__data\.json/.test(event.meta.page.url)) {
          return null;
        }
        return event;
      },
    });
  } catch {
    console.warn("[Faro] Failed to initialize — telemetry disabled");
  }

  return faro;
}

/**
 * Get the current Faro instance (undefined if not initialized or SSR).
 */
export function getFaro(): Faro | undefined {
  return faro;
}

/**
 * Set user identity on the Faro instance for session correlation.
 * Only sends user ID + username — no PII (email, real name) in telemetry logs.
 */
export function setFaroUser(userId: string, username?: string): void {
  faro?.api.setUser({ id: userId, username: username ?? undefined });
}

/**
 * Clear user identity (e.g., on sign-out).
 */
export function resetFaroUser(): void {
  faro?.api.resetUser();
}
