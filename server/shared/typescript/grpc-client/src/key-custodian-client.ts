// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { AuthFailures } from "@d2/auth-abstractions";
import type { ILogger } from "@d2/logging";
import { Singleflight } from "@d2/resilience";
import type { D2Result } from "@d2/result";
import { ok } from "@d2/result";
import { falsey } from "@d2/utilities";
import type { InternalTokenSnapshot } from "./types.js";

/**
 * Configuration for the HTTP-based KeyCustodian client.
 */
export interface HttpKeyCustodianClientOptions {
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
 * Pluggable contract for any KeyCustodian token-acquire backend. The
 * interceptor depends on this interface, NOT the concrete HTTP client —
 * tests inject a mock; production wires the HTTP client.
 */
export interface KeyCustodianClient {
  acquireToken(): Promise<D2Result<InternalTokenSnapshot>>;
}

/**
 * HTTP implementation of KeyCustodianClient using Node-native `fetch()`.
 * Singleflight-deduped so 100 concurrent gRPC calls all force-refreshing
 * after a 401 trigger ONLY ONE upstream KeyCustodian call.
 */
export class HttpKeyCustodianClient implements KeyCustodianClient {
  private readonly singleflight = new Singleflight<
    string,
    D2Result<InternalTokenSnapshot>
  >();
  private readonly opts: Required<
    Omit<HttpKeyCustodianClientOptions, "logger" | "fetchImpl">
  > & {
    fetchImpl: typeof fetch;
    logger: ILogger | undefined;
  };

  constructor(opts: HttpKeyCustodianClientOptions) {
    if (falsey(opts.tokenEndpoint)) {
      throw new TypeError("HttpKeyCustodianClient: tokenEndpoint required");
    }
    if (falsey(opts.clientId)) {
      throw new TypeError("HttpKeyCustodianClient: clientId required");
    }
    if (falsey(opts.clientSecret)) {
      throw new TypeError("HttpKeyCustodianClient: clientSecret required");
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
  }

  acquireToken(): Promise<D2Result<InternalTokenSnapshot>> {
    return this.singleflight.do("internal-token", () => this.fetchToken());
  }

  /** Singleflight inflight count — for tests + observability. */
  get inflightCount(): number {
    return this.singleflight.inflightCount;
  }

  private async fetchToken(): Promise<D2Result<InternalTokenSnapshot>> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), this.opts.timeoutMs);
    try {
      const body = new URLSearchParams({
        grant_type: "client_credentials",
        client_id: this.opts.clientId,
        client_secret: this.opts.clientSecret,
        audience: this.opts.audience,
      });
      const res = await this.opts.fetchImpl(this.opts.tokenEndpoint, {
        method: "POST",
        headers: { "Content-Type": "application/x-www-form-urlencoded" },
        body: body.toString(),
        signal: controller.signal,
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
      const snapshot = _validateTokenResponse(parsed, this.opts.audience);
      if (snapshot === null) {
        this.opts.logger?.warn("internal-token response shape invalid", {
          endpoint: this.opts.tokenEndpoint,
        });
        return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
      }
      return ok(snapshot);
    } catch (err) {
      this.opts.logger?.warn("internal-token acquire threw", {
        endpoint: this.opts.tokenEndpoint,
        errorName: err instanceof Error ? err.name : "unknown",
      });
      return AuthFailures.jwksUnavailable() as D2Result<InternalTokenSnapshot>;
    } finally {
      clearTimeout(timer);
    }
  }
}

const _MAX_TOKEN_LENGTH = 8 * 1024;

function _validateTokenResponse(
  parsed: unknown,
  expectedAudience: string,
): InternalTokenSnapshot | null {
  if (parsed === null || typeof parsed !== "object" || Array.isArray(parsed)) {
    return null;
  }
  const obj = parsed as Record<string, unknown>;
  const accessToken = obj["access_token"];
  if (typeof accessToken !== "string" || accessToken.length === 0) return null;
  if (accessToken.length > _MAX_TOKEN_LENGTH) return null;
  const expiresIn = obj["expires_in"];
  if (
    typeof expiresIn !== "number" ||
    !Number.isFinite(expiresIn) ||
    expiresIn <= 0
  ) {
    return null;
  }
  return {
    accessToken,
    expiresAtMs: Date.now() + expiresIn * 1000,
    audience: expectedAudience,
  };
}
