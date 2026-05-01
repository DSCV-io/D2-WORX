import type { HandleClientError } from "@sveltejs/kit";
import { initFaro, getFaro } from "$lib/client/telemetry/faro.js";

// Initialize Faro on app startup (client-side only).
initFaro();

export const handleError: HandleClientError = async ({ error, status, message }) => {
  const traceId = crypto.randomUUID();

  const faro = getFaro();
  if (faro) {
    const err = error instanceof Error ? error : new Error(String(error));
    faro.api.pushError(err, {
      context: { status: String(status), traceId },
    });
  } else {
    // Fallback when Faro is unavailable
    console.error(
      `[Client Error] ${status}: ${error instanceof Error ? error.message : String(error)}`,
      { traceId },
    );
  }

  return {
    message,
    traceId,
  };
};
