import { dev } from "$app/environment";
import { error } from "@sveltejs/kit";
import type { PageServerLoad } from "./$types";
import { getGatewayContext } from "$lib/server/rest/gateway.server";

interface ComponentHealth {
  status: string;
  latencyMs: number;
  error?: string | null;
}

interface ServiceHealth {
  status: string;
  latencyMs: number;
  components: Record<string, ComponentHealth>;
}

interface HealthResponse {
  status: string;
  timestamp: string;
  services: Record<string, ServiceHealth>;
}

export const load: PageServerLoad = async () => {
  if (!dev) {
    error(404, "Not found");
  }

  // The health endpoint returns raw JSON (not a D2Result envelope),
  // so we fetch directly instead of using gatewayFetchAnon.
  const ctx = getGatewayContext();
  const url = `${ctx.baseUrl}/api/health`;

  try {
    const response = await fetch(url, {
      signal: AbortSignal.timeout(15_000),
    });

    const health: HealthResponse = await response.json();

    return {
      health,
      error: null,
    };
  } catch (e) {
    return {
      health: null,
      error: e instanceof Error ? e.message : "Gateway unreachable",
    };
  }
};
