// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  type D2Result,
  ok,
  canceled,
  serviceUnavailable,
  bubbleFail,
} from "@dcsv-io/d2-result";
import { CircuitBreaker, Singleflight } from "@dcsv-io/d2-resilience";
import { sanitizedErrorRender, type ILogger } from "@dcsv-io/d2-logging";
import type { ChannelCredentials } from "@grpc/grpc-js";
import type { WorkloadCertificateIssuer } from "./workload-certificate-issuer.js";
import type { CaTrustBundle, LeafSnapshot } from "./workload-leaf-material.js";
import type { WorkloadLeafClientOptions } from "./leaf-client-options.js";
import { WorkloadLeafCache } from "./leaf-cache.js";
import { generateLeafKeypair } from "./leaf-keypair.js";
import { buildCsr, MAX_CSR_DER_BYTES } from "./csr-builder.js";
import { leafMatchesLocalKey } from "./leaf-key-match.js";
import { derToPem } from "./der-pem.js";
import { assembleTrustStore } from "./trust-assembly.js";
import { buildMutualTlsCredentials } from "./mtls-channel.js";

// Exactly one "reissue this workload's leaf" operation per process — the
// singleflight key is a constant so on-demand + refresh-ahead callers dedup.
const _SINGLEFLIGHT_KEY = "workload-leaf";

// Refresh-ahead margin + circuit defaults, mirroring the .NET twins verbatim.
// Provenance (value): AuthOutboundOptions.WorkloadLeafRefreshLeadTime = 5 min;
// AuthOutboundResilienceDefaults.FAILURE_THRESHOLD = 5; .SR_CooldownDuration = 30 s.
// Dual drift-guard: workload-leaf-defaults.test.ts pins these literals here and the
// .NET AuthOutboundDefaultsParityTests pins the twin - divergence reds a pin test.
export const DEFAULT_REFRESH_MARGIN_MS = 5 * 60 * 1000;
export const DEFAULT_FAILURE_THRESHOLD = 5;
export const DEFAULT_COOLDOWN_MS = 30 * 1000;

/**
 * Refresh-ahead workload-leaf client — the TS twin of the .NET
 * `WorkloadLeafClient`. Generates a FRESH ECDSA P-256 keypair per reissue (the
 * private key never leaves the process), builds a PKCS#10 CSR, obtains a signed
 * leaf through the injected {@link WorkloadCertificateIssuer} port, verifies the
 * returned leaf certifies the local key (mismatch defense), caches the presentable
 * leaf until it nears expiry, and reissues under a single-flight + circuit-breaker
 * with serve-stale-on-transient semantics. Also fetches + caches the CA trust
 * bundle and assembles the mutual-TLS channel credentials the workload presents.
 *
 * The private key never crosses the issuer seam — only the CSR (public material)
 * does. A returned leaf whose public key does not match the local keypair is
 * rejected before any cache write (there is no private key for it), so the
 * still-valid cached leaf keeps serving.
 */
export class WorkloadLeafClient {
  readonly #issuer: WorkloadCertificateIssuer;
  readonly #cache = new WorkloadLeafCache();
  readonly #singleflight = new Singleflight<string, boolean>();
  readonly #circuitBreaker: CircuitBreaker<boolean>;
  readonly #now: () => number;
  readonly #refreshMarginMs: number;
  readonly #logger: ILogger | undefined;
  #caTrustBundle: CaTrustBundle | undefined;
  #disposed = false;

  /**
   * @param issuer  - The transport port that issues leaves + fetches the CA chain.
   * @param options - Tunables (clock, refresh margin, circuit thresholds, logger).
   */
  constructor(
    issuer: WorkloadCertificateIssuer,
    options: WorkloadLeafClientOptions = {},
  ) {
    this.#issuer = issuer;
    this.#now = options.now ?? Date.now;
    this.#refreshMarginMs =
      options.refreshMarginMs ?? DEFAULT_REFRESH_MARGIN_MS;
    this.#logger = options.logger;
    this.#circuitBreaker = new CircuitBreaker<boolean>({
      failureThreshold:
        options.circuitFailureThreshold ?? DEFAULT_FAILURE_THRESHOLD,
      cooldownMs: options.circuitCooldownMs ?? DEFAULT_COOLDOWN_MS,
      isFailure: (r) => !r,
      nowFunc: this.#now,
    });
  }

  /**
   * Return the current presentable leaf, reissuing when the cache is empty /
   * expired or within the refresh-ahead margin. A still-valid cached leaf is
   * served even when the reissue fails (serve-stale). Returns a typed
   * `serviceUnavailable` when no valid leaf can be produced, or `canceled` when
   * the caller's signal aborts.
   */
  async getCurrentLeaf(signal?: AbortSignal): Promise<D2Result<LeafSnapshot>> {
    this.#throwIfDisposed();

    const nowMs = this.#now();
    const cached = this.#cache.tryGet(nowMs);

    if (cached !== undefined && !this.#needsRefresh(cached, nowMs))
      return ok(cached);

    return this.#reissueThenRead(signal);
  }

  /**
   * Force a reissue (the proactive-refresh entry point), going through the same
   * single-flight as on-demand callers. Returns `ok` when a valid leaf is present
   * afterward, else `serviceUnavailable` / `canceled`.
   */
  async forceReissue(signal?: AbortSignal): Promise<D2Result<void>> {
    this.#throwIfDisposed();

    const result = await this.#reissueThenRead(signal);

    return result.success ? ok() : bubbleFail<void>(result);
  }

  /**
   * Fetch + cache the CA trust bundle (root + issuing intermediate) the workload
   * pins when validating the mutual-TLS server. Cached for the client's lifetime
   * — the CA chain is long-lived trust material.
   */
  async getCaTrustBundle(
    signal?: AbortSignal,
  ): Promise<D2Result<CaTrustBundle>> {
    this.#throwIfDisposed();

    if (this.#caTrustBundle !== undefined) return ok(this.#caTrustBundle);

    const chain = await this.#issuer.getCaCertificate(signal);

    if (chain.failed) return bubbleFail<CaTrustBundle>(chain);

    // A success envelope with no data is a malformed response — typed retryable
    // failure, never an empty trust store.
    if (chain.data === undefined) return serviceUnavailable<CaTrustBundle>();

    const bundle = assembleTrustStore(chain.data);
    this.#caTrustBundle = bundle;

    return ok(bundle);
  }

  /**
   * Assemble mutual-TLS channel credentials presenting the current leaf chain +
   * private key and pinning the fetched CA bundle — the workload's client-side
   * mutual-TLS presentation. Reissues / fetches as needed.
   */
  async currentChannelCredentials(
    signal?: AbortSignal,
  ): Promise<D2Result<ChannelCredentials>> {
    const leaf = await this.getCurrentLeaf(signal);

    if (leaf.failed) return bubbleFail<ChannelCredentials>(leaf);

    const trust = await this.getCaTrustBundle(signal);

    if (trust.failed) return bubbleFail<ChannelCredentials>(trust);

    const credentials = buildMutualTlsCredentials({
      caBundlePem: trust.data!.caBundlePem,
      certChainPem: leaf.data!.certChainPem,
      privateKeyPem: leaf.data!.privateKeyPem,
    });

    return ok(credentials);
  }

  /** Marks the client disposed — subsequent calls throw. */
  dispose(): void {
    this.#disposed = true;
  }

  // -------------------------------------------------------------------------
  // Internals
  // -------------------------------------------------------------------------

  async #reissueThenRead(
    signal?: AbortSignal,
  ): Promise<D2Result<LeafSnapshot>> {
    try {
      await this.#singleflight.do(_SINGLEFLIGHT_KEY, () =>
        this.#circuitBreaker.execute(() => this.#reissue(signal)),
      );
    } catch (e) {
      // Abort → surface cancellation. The only other exception the
      // single-flight + breaker pipeline raises is CircuitOpenError (fast-fail
      // while open); swallow it and fall through to the read-through so a
      // still-valid cached leaf is served stale.
      if (this.#isAbort(e, signal)) return canceled<LeafSnapshot>();
    }

    const current = this.#cache.tryGet(this.#now());

    if (current !== undefined) return ok(current);

    return serviceUnavailable<LeafSnapshot>();
  }

  async #reissue(signal?: AbortSignal): Promise<boolean> {
    // A sibling caller may have published a FRESH leaf between the caller's tryGet
    // and the single-flight entry — short-circuit only for a leaf that does NOT
    // itself need refreshing (a still-within-margin leaf must proceed to reissue,
    // else the refresh-ahead cycle would never fire).
    const preReissue = this.#cache.tryGet(this.#now());

    if (
      preReissue !== undefined &&
      !this.#needsRefresh(preReissue, this.#now())
    )
      return true;

    try {
      // Fresh keypair per reissue — rotation freshness holds because a new key is
      // minted every cycle; the private key never crosses the issuer seam.
      const keypair = await generateLeafKeypair();
      const csrDer = await buildCsr(keypair.cryptoKeyPair);

      /* v8 ignore start -- a real P-256 CSR is well under the 4 KiB cap; the client-side pre-check is defensive (the server enforces the same cap) */
      if (csrDer.byteLength > MAX_CSR_DER_BYTES) {
        this.#logReissueFailure(
          new Error("csr-exceeds-cap"),
          "csr-exceeds-cap",
        );
        return false;
      }
      /* v8 ignore stop */

      const issuance = await this.#issuer.issueLeaf(csrDer, signal);

      if (issuance.failed || issuance.data === undefined) return false;

      const material = issuance.data;

      // Mismatch defense: a leaf certifying a DIFFERENT key can never be
      // presented (no private key for it) — reject before any cache write.
      if (!leafMatchesLocalKey(material.certificateDer, keypair.spkiDer)) {
        this.#logKeyMismatch();
        return false;
      }

      const privateKeyPem = await keypair.exportPrivateKeyPkcs8Pem();
      const certChainPem =
        derToPem(material.certificateDer, "CERTIFICATE") +
        derToPem(material.issuerCertificateDer, "CERTIFICATE");

      this.#cache.set({
        certChainPem,
        privateKeyPem,
        notAfter: material.notAfter,
      });

      return true;
    } catch (e) {
      // Abort propagates (the outer pipeline maps it to cancellation). Any other
      // error — keygen / CSR / issuer / cert parse / export — is transient: the
      // cached leaf keeps serving while the next reissue may succeed.
      if (this.#isAbort(e, signal)) throw e;

      this.#logReissueFailure(e, undefined);
      return false;
    }
  }

  #needsRefresh(cached: LeafSnapshot, nowMs: number): boolean {
    return cached.notAfter.epochMilliseconds - this.#refreshMarginMs <= nowMs;
  }

  #isAbort(_e: unknown, signal: AbortSignal | undefined): boolean {
    return signal?.aborted === true;
  }

  #throwIfDisposed(): void {
    if (this.#disposed) throw new Error("WorkloadLeafClient has been disposed");
  }

  #logKeyMismatch(): void {
    // Never logs key material — a fixed operator-meaningful message only.
    this.#logger?.warn(
      "Workload leaf reissue rejected: issuer returned a leaf certifying a different key",
    );
  }

  #logReissueFailure(error: unknown, reason: string | undefined): void {
    const stale = this.#cache.peekRaw();
    const cachedLeafNotAfter =
      stale !== undefined ? stale.notAfter.toString() : "none";

    // Sanitized error shape only — never the raw error (a cert-parse error could
    // echo subject / SAN content); never key material.
    const render = sanitizedErrorRender(error);

    this.#logger?.error("Workload leaf reissue failed", {
      cachedLeafNotAfter,
      reason,
      errorName: render.name,
      errorFrame: render.firstFrame,
    });
  }
}
