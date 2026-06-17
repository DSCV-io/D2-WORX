// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthFailures } from "@d2/auth-abstractions";
import type { ILogger } from "@d2/logging";
import {
  type ResilientPipeline,
  ResilientPipelineBuilder,
} from "@d2/resilience";
import type { D2Result } from "@d2/result";
import { ok } from "@d2/result";
import { falsey } from "@d2/utilities";
import type { InternalTokenSnapshot } from "./types.js";

/**
 * Configuration for the HTTP-based internal-token client that acquires the
 * BFF's service-identity token from the OAuth token endpoint
 * (`grant_type=client_credentials`).
 */
export interface HttpInternalTokenClientOptions {
  /** Token endpoint URL (full path). Required. */
  readonly tokenEndpoint: string;
  /** OAuth client_id for the BFF service identity. Required. */
  readonly clientId: string;
  /** OAuth client_secret for the BFF. Required. */
  readonly clientSecret: string;
  /** Audience to request the token for. Defaults to `d2.edge`. */
  readonly audience?: string;
  /** Optional fetch implementation override (test seam). */
  readonly fetchImpl?: typeof fetch;
  /** Optional logger for diagnostic events (token bytes are NEVER logged). */
  readonly logger?: ILogger;
  /** Request timeout in ms. Defaults to 5_000. */
  readonly timeoutMs?: number;
}

/**
 * Pluggable contract for any internal-token acquire backend. The interceptor
 * depends on this interface, NOT the concrete HTTP client — tests inject a
 * mock; production wires the HTTP client.
 *
 * "Internal token" means the BFF's own service-identity JWT, minted by the
 * OAuth token endpoint (`grant_type=client_credentials`), NOT a user-facing
 * token.
 */
export interface InternalTokenClient {
  acquireToken(): Promise<D2Result<InternalTokenSnapshot>>;
}

/**
 * HTTP implementation of {@link InternalTokenClient} using Node-native
 * `fetch()`. Calls the OAuth token endpoint
 * (`grant_type=client_credentials`) to acquire the BFF's service-identity
 * JWT for use as `Authorization: Bearer <jwt>` on outbound gRPC calls.
 *
 * Resilience is composed from `@d2/resilience`: a Singleflight layer (so 100
 * concurrent gRPC calls all force-refreshing after a 401 trigger ONLY ONE
 * upstream call) wraps a TimeoutLayer that bounds the request AND — via the
 * threaded `AbortSignal` passed into `fetch` — genuinely cancels it
 * (releasing the socket) on timeout. Server-side / SSR only.
 */
export class HttpInternalTokenClient implements InternalTokenClient {
  private readonly pipeline: ResilientPipeline;
  private readonly opts: Required<
    Omit<HttpInternalTokenClientOptions, "logger" | "fetchImpl">
  > & {
    fetchImpl: typeof fetch;
    logger: ILogger | undefined;
  };

  // Reflects whether a real upstream fetch is currently in flight (the
  // Singleflight layer dedups, so this is 0 or 1). Test + observability probe.
  private inflight = 0;

  constructor(opts: HttpInternalTokenClientOptions) {
    if (falsey(opts.tokenEndpoint)) {
      throw new TypeError("HttpInternalTokenClient: tokenEndpoint required");
    }
    if (falsey(opts.clientId)) {
      throw new TypeError("HttpInternalTokenClient: clientId required");
    }
    if (falsey(opts.clientSecret)) {
      throw new TypeError("HttpInternalTokenClient: clientSecret required");
    }
    this.opts = {
      tokenEndpoint: opts.tokenEndpoint,
      clientId: opts.clientId,
      clientSecret: opts.clientSecret,
      audience: opts.audience ?? "d2.edge",
      timeoutMs: opts.timeoutMs ?? 5_000,
      fetchImpl: opts.fetchImpl ?? fetch,
      logger: opts.logger,
    };
    this.pipeline = new ResilientPipelineBuilder()
      .useSingleflight()
      .useTimeout({ durationMs: this.opts.timeoutMs })
      .build();
  }

  acquireToken(): Promise<D2Result<InternalTokenSnapshot>> {
    // The pipeline THROWS on timeout (TimeoutError) — map that boundary to the
    // same jwksUnavailable failure the inner fetch paths return.
    return this.pipeline
      .execute("internal-token", (signal) => this.fetchToken(signal))
      .catch((err: unknown) => {
        this.opts.logger?.warn("internal-token acquire threw", {
          endpoint: this.opts.tokenEndpoint,
          errorName: err instanceof Error ? err.name : "unknown",
        });
        return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
      });
  }

  /** Inflight count (0 or 1 — Singleflight dedups) for tests + observability. */
  get inflightCount(): number {
    return this.inflight;
  }

  private async fetchToken(
    signal?: AbortSignal,
  ): Promise<D2Result<InternalTokenSnapshot>> {
    this.inflight++;
    try {
      const body = new URLSearchParams({
        grant_type: "client_credentials",
        client_id: this.opts.clientId,
        client_secret: this.opts.clientSecret,
        audience: this.opts.audience,
      });
      // The threaded signal is the TimeoutLayer's linked signal — passing it to
      // fetch makes the timeout actually abort the in-flight request.
      const res = await this.opts.fetchImpl(this.opts.tokenEndpoint, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: body.toString(),
        signal,
      });
      if (!res.ok) {
        this.opts.logger?.warn("internal-token acquire failed", {
          httpStatus: res.status,
          endpoint: this.opts.tokenEndpoint,
        });
        return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
      }
      let parsed: unknown;
      try {
        parsed = await res.json();
      } catch {
        this.opts.logger?.warn("internal-token response not JSON", {
          endpoint: this.opts.tokenEndpoint,
        });
        return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
      }
      const snap = _validateTokenResponse(parsed, this.opts.audience);
      if (snap === undefined) {
        this.opts.logger?.warn("internal-token response shape invalid", {
          endpoint: this.opts.tokenEndpoint,
        });
        return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
      }
      return ok(snap);
    } finally {
      this.inflight--;
    }
  }
}

const _MAX_TOKEN_LENGTH = 8 * 1024;

function _validateTokenResponse(
  parsed: unknown,
  expectedAudience: string,
): InternalTokenSnapshot | undefined {
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    return undefined;
  }
  const obj = parsed as Record<string, unknown>;
  const accessToken = obj["access_token"];
  if (typeof accessToken !== "string" || accessToken.length === 0)
    return undefined;
  if (accessToken.length > _MAX_TOKEN_LENGTH) return undefined;
  const expiresIn = obj["expires_in"];
  if (
    typeof expiresIn !== "number" ||
    !Number.isFinite(expiresIn) ||
    expiresIn <= 0
  ) {
    return undefined;
  }
  return {
    accessToken,
    expiresAtMs: Date.now() + expiresIn * 1000,
    audience: expectedAudience,
  };
}
