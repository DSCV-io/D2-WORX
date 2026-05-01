/**
 * Client-side utility for calling custom auth service endpoints.
 *
 * Custom auth endpoints (e.g. check-email) return D2Result responses
 * and go through SvelteKit's /api/auth/* proxy to the Auth service.
 *
 * Uses the same timeout / error-handling / D2Result-parsing patterns
 * as gateway-client.ts, but targets the auth proxy (relative URLs)
 * instead of the .NET gateway (PUBLIC_GATEWAY_URL).
 *
 * For BetterAuth operations (signUp, signIn, etc.), use authClient instead.
 */
import { D2Result } from "@d2/result";
import { executeFetch } from "$lib/shared/rest/gateway-response.js";

const DEFAULT_TIMEOUT_MS = 10_000;

export interface AuthApiCallOptions {
  method?: "GET" | "POST";
  body?: unknown;
  signal?: AbortSignal;
  timeout?: number;
}

/**
 * Call a custom auth service endpoint that returns D2Result.
 *
 * Requests are sent to the SvelteKit auth proxy at `/api/auth/*`,
 * which forwards them to the Auth service with full cookie/header
 * preservation through the security middleware pipeline.
 *
 * @param path - Auth endpoint path (e.g. "/api/auth/check-email?email=...")
 * @param options - Optional request configuration
 */
export async function authApiCall<TData = void>(
  path: string,
  options?: AuthApiCallOptions,
): Promise<D2Result<TData>> {
  const headers = new Headers();
  if (options?.body !== undefined) {
    headers.set("Content-Type", "application/json");
  }

  return executeFetch<TData>(path, {
    method: options?.method,
    headers,
    body: options?.body !== undefined ? JSON.stringify(options.body) : undefined,
    signal: options?.signal,
    timeout: options?.timeout ?? DEFAULT_TIMEOUT_MS,
    credentials: "include",
  });
}
