// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Server-side Edge gateway context for BFF loaders (debug health, etc.).
 *
 * Base URL prefers public env (browser-safe Edge HTTP origin). Defaults match
 * local Compose host ports from `.env.local.example` (EDGE_PORT=8080).
 */

import { env as publicEnv } from "$env/dynamic/public";

export type GatewayContext = {
  readonly baseUrl: string;
};

/**
 * Resolve Edge HTTP base URL for server-side fetches.
 */
export function getGatewayContext(): GatewayContext {
  const fromEnv =
    publicEnv.PUBLIC_GATEWAY_URL ??
    publicEnv.PUBLIC_EDGE_HTTP_URL ??
    publicEnv.PUBLIC_EDGE_BASE_URL ??
    publicEnv.PUBLIC_API_BASE_URL;

  const baseUrl =
    typeof fromEnv === "string" && fromEnv.trim().length > 0
      ? fromEnv.replace(/\/$/, "")
      : "http://127.0.0.1:8080";

  return { baseUrl };
}
