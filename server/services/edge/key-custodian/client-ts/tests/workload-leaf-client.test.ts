// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Behavioral suite for WorkloadLeafClient — the TS twin of the .NET
// WorkloadLeafClientTests: fresh-keypair-per-reissue, CSR-only wire, mismatch
// defense, cache + refresh-ahead + serve-stale, single-flight dedup, circuit
// fast-fail, CA trust caching, mutual-TLS credential assembly, cancellation,
// disposal, and the structural secret pins (no key material on the issuer seam
// or in log captures).

import { describe, expect, it } from "vitest";
import { Temporal } from "temporal-polyfill";
import { ok, serviceUnavailable } from "@d2/result";
import { Pkcs10CertificateRequest } from "@peculiar/x509";
import type { ILogger, LogBindings } from "@d2/logging";
import { WorkloadLeafClient } from "../src/issuance/workload-leaf-client.js";
import type { WorkloadLeafMaterial } from "../src/issuance/workload-leaf-material.js";
import { FakeWorkloadCertificateIssuer } from "./support/fake-workload-certificate-issuer.js";
import { TestCertificateAuthority } from "./support/test-certificate-authority.js";

const HOUR_MS = 3600_000;

/** A capturing ILogger — every message + bindings recorded for the secret pins. */
class CapturingLogger implements ILogger {
  readonly entries: { message: string; bindings?: LogBindings }[] = [];

  trace(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  debug(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  info(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  warn(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  error(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  fatal(message: string, bindings?: LogBindings): void {
    this.entries.push({ message, bindings });
  }

  child(): ILogger {
    return this;
  }

  allText(): string {
    return this.entries
      .map((e) => e.message + JSON.stringify(e.bindings ?? {}))
      .join("\n");
  }
}

/** An issuer script that signs the REAL CSR via the test CA (mismatch defense passes). */
function caBackedIssuer(
  ca: TestCertificateAuthority,
  notAfter: () => Date,
): FakeWorkloadCertificateIssuer {
  return new FakeWorkloadCertificateIssuer({
    issue: async (csrDer) => ok(await ca.issueLeafFromCsr(csrDer, notAfter())),
    getCa: async () => ok(ca.caChain()),
  });
}

describe("WorkloadLeafClient — issuance + cache", () => {
  it("issues on first call, sends ONLY a parseable PKCS#10 CSR, and caches the leaf", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const first = await client.getCurrentLeaf();

    expect(first.success).toBe(true);
    expect(first.data!.certChainPem).toContain("-----BEGIN CERTIFICATE-----");
    expect(first.data!.privateKeyPem).toContain("-----BEGIN PRIVATE KEY-----");

    // The seam saw exactly one request and it was a REAL self-signed PKCS#10 CSR
    // — nothing else crossed (structural: the port signature admits only csrDer).
    expect(issuer.csrsSeen).toHaveLength(1);
    const csr = new Pkcs10CertificateRequest(
      toArrayBuffer(issuer.csrsSeen[0]!),
    );
    await expect(csr.verify()).resolves.toBe(true);

    // Second call serves from cache — no second issuance.
    const second = await client.getCurrentLeaf();
    expect(second.success).toBe(true);
    expect(second.data).toBe(first.data);
    expect(issuer.issueCallCount).toBe(1);
  });

  it("mints a FRESH keypair per reissue — consecutive leaves certify different keys", async () => {
    const ca = await TestCertificateAuthority.create();
    let now = 1_000_000;
    const issuer = caBackedIssuer(ca, () => new Date(now + HOUR_MS));
    const client = new WorkloadLeafClient(issuer, { now: () => now });

    const first = await client.getCurrentLeaf();
    expect(first.success).toBe(true);

    // Expire the cached leaf, forcing a reissue with a new keypair.
    now += 2 * HOUR_MS;
    const second = await client.getCurrentLeaf();
    expect(second.success).toBe(true);

    expect(issuer.issueCallCount).toBe(2);
    expect(second.data!.privateKeyPem).not.toBe(first.data!.privateKeyPem);

    // The two CSRs carry different public keys (per-rotation key freshness).
    const csr1 = new Pkcs10CertificateRequest(
      toArrayBuffer(issuer.csrsSeen[0]!),
    );
    const csr2 = new Pkcs10CertificateRequest(
      toArrayBuffer(issuer.csrsSeen[1]!),
    );
    expect(Buffer.from(csr1.publicKey.rawData).toString("base64")).not.toBe(
      Buffer.from(csr2.publicKey.rawData).toString("base64"),
    );
  });

  it("refresh-ahead: a within-margin leaf triggers reissue and the NEW leaf is served", async () => {
    const ca = await TestCertificateAuthority.create();
    let now = 1_000_000;
    // First leaf expires in 10 minutes; margin is 5 minutes.
    let validity = 10 * 60 * 1000;
    const issuer = caBackedIssuer(ca, () => new Date(now + validity));
    const client = new WorkloadLeafClient(issuer, {
      now: () => now,
      refreshMarginMs: 5 * 60 * 1000,
    });

    const first = await client.getCurrentLeaf();
    expect(first.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);

    // Advance to 6 minutes before expiry — outside the margin: cache serves.
    now += 4 * 60 * 1000;
    await client.getCurrentLeaf();
    expect(issuer.issueCallCount).toBe(1);

    // Advance to 4 minutes before expiry — inside the margin: proactive reissue.
    now += 2 * 60 * 1000;
    validity = HOUR_MS;
    const refreshed = await client.getCurrentLeaf();
    expect(issuer.issueCallCount).toBe(2);
    expect(refreshed.success).toBe(true);
    expect(refreshed.data).not.toBe(first.data);
  });

  it("serve-stale: a still-valid cached leaf keeps serving when the reissue fails", async () => {
    const ca = await TestCertificateAuthority.create();
    let now = 1_000_000;
    let failIssue = false;
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (csrDer) => {
        if (failIssue) return serviceUnavailable<WorkloadLeafMaterial>();
        return ok(
          await ca.issueLeafFromCsr(csrDer, new Date(now + 10 * 60 * 1000)),
        );
      },
    });
    const client = new WorkloadLeafClient(issuer, {
      now: () => now,
      refreshMarginMs: 5 * 60 * 1000,
    });

    const first = await client.getCurrentLeaf();
    expect(first.success).toBe(true);

    // Enter the refresh margin with a failing issuer — the reissue fails but the
    // still-valid cached leaf is served stale.
    failIssue = true;
    now += 6 * 60 * 1000;
    const stale = await client.getCurrentLeaf();
    expect(stale.success).toBe(true);
    expect(stale.data).toBe(first.data);
    expect(issuer.issueCallCount).toBe(2);
  });

  it("returns serviceUnavailable when no leaf can be produced at all", async () => {
    const issuer = new FakeWorkloadCertificateIssuer(); // always unavailable
    const client = new WorkloadLeafClient(issuer);

    const result = await client.getCurrentLeaf();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
  });
});

describe("WorkloadLeafClient — mismatch defense", () => {
  it("rejects a leaf certifying a DIFFERENT key before any cache write", async () => {
    const ca = await TestCertificateAuthority.create();
    const logger = new CapturingLogger();
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async () =>
        ok(await ca.issueLeafOverForeignKey(new Date(Date.now() + HOUR_MS))),
    });
    const client = new WorkloadLeafClient(issuer, { logger });

    const result = await client.getCurrentLeaf();

    // The foreign leaf never enters the cache — no leaf is served.
    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
    expect(
      logger.entries.some((e) => e.message.includes("different key")),
    ).toBe(true);
  });

  it("keeps serving the still-valid cached leaf when a reissue returns a mismatched leaf", async () => {
    const ca = await TestCertificateAuthority.create();
    let now = 1_000_000;
    let returnForeign = false;
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (csrDer) => {
        const notAfter = new Date(now + 10 * 60 * 1000);
        if (returnForeign)
          return ok(await ca.issueLeafOverForeignKey(notAfter));
        return ok(await ca.issueLeafFromCsr(csrDer, notAfter));
      },
    });
    const client = new WorkloadLeafClient(issuer, {
      now: () => now,
      refreshMarginMs: 5 * 60 * 1000,
    });

    const first = await client.getCurrentLeaf();
    expect(first.success).toBe(true);

    returnForeign = true;
    now += 6 * 60 * 1000; // inside the margin → reissue attempt → mismatch reject
    const stale = await client.getCurrentLeaf();

    expect(stale.success).toBe(true);
    expect(stale.data).toBe(first.data); // the mismatched leaf never replaced it
  });

  it("treats an unparseable returned certificate as a transient failure", async () => {
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async () =>
        ok({
          certificateDer: new Uint8Array([0xba, 0xad]),
          issuerCertificateDer: new Uint8Array([0xf0, 0x0d]),
          notAfter: Temporal.Instant.fromEpochMilliseconds(
            Date.now() + HOUR_MS,
          ),
        }),
    });
    const logger = new CapturingLogger();
    const client = new WorkloadLeafClient(issuer, { logger });

    const result = await client.getCurrentLeaf();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
    // The failure was logged sanitized (name + frame), never the raw error text.
    expect(
      logger.entries.some((e) => e.message.includes("reissue failed")),
    ).toBe(true);
  });
});

describe("WorkloadLeafClient — concurrency + circuit", () => {
  it("single-flight: concurrent first calls dedup to ONE issuance", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const [a, b, c] = await Promise.all([
      client.getCurrentLeaf(),
      client.getCurrentLeaf(),
      client.getCurrentLeaf(),
    ]);

    expect(a.success && b.success && c.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);
    expect(b.data).toBe(a.data);
    expect(c.data).toBe(a.data);
  });

  it("circuit fast-fails after the failure threshold, then recovers after cooldown", async () => {
    const ca = await TestCertificateAuthority.create();
    let now = 1_000_000;
    let healthy = false;
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (csrDer) => {
        if (!healthy) return serviceUnavailable<WorkloadLeafMaterial>();
        return ok(await ca.issueLeafFromCsr(csrDer, new Date(now + HOUR_MS)));
      },
    });
    const client = new WorkloadLeafClient(issuer, {
      now: () => now,
      circuitFailureThreshold: 2,
      circuitCooldownMs: 30_000,
    });

    // Two failures open the circuit.
    expect((await client.getCurrentLeaf()).failed).toBe(true);
    expect((await client.getCurrentLeaf()).failed).toBe(true);
    expect(issuer.issueCallCount).toBe(2);

    // Circuit open → fast-fail without touching the issuer.
    expect((await client.getCurrentLeaf()).failed).toBe(true);
    expect(issuer.issueCallCount).toBe(2);

    // After the cooldown the half-open probe reaches the now-healthy issuer.
    healthy = true;
    now += 31_000;
    const recovered = await client.getCurrentLeaf();
    expect(recovered.success).toBe(true);
    expect(issuer.issueCallCount).toBe(3);
  });

  it("forceReissue goes through the same path and reports ok on success", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const forced = await client.forceReissue();

    expect(forced.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);

    // The forced leaf serves subsequent reads from cache.
    const read = await client.getCurrentLeaf();
    expect(read.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);
  });

  it("forceReissue short-circuits when a FRESH leaf is already cached (sibling-converge)", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    // Populate the cache with a fresh (outside-margin) leaf.
    const first = await client.getCurrentLeaf();
    expect(first.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);

    // forceReissue's in-flight pre-check observes the fresh leaf and converges on
    // it — the .NET pre-reissue cache re-check twin (no redundant issuance).
    const forced = await client.forceReissue();
    expect(forced.success).toBe(true);
    expect(issuer.issueCallCount).toBe(1);
  });

  it("forceReissue surfaces a typed failure when issuance is down", async () => {
    const client = new WorkloadLeafClient(new FakeWorkloadCertificateIssuer());

    const forced = await client.forceReissue();

    expect(forced.failed).toBe(true);
    expect(forced.statusCode).toBe(503);
  });
});

describe("WorkloadLeafClient — CA trust + channel credentials", () => {
  it("fetches the CA chain once and caches the assembled trust bundle", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const first = await client.getCaTrustBundle();
    const second = await client.getCaTrustBundle();

    expect(first.success).toBe(true);
    expect(first.data!.caBundlePem).toContain("-----BEGIN CERTIFICATE-----");
    expect(second.data).toBe(first.data);
    expect(issuer.caCallCount).toBe(1);
  });

  it("bubbles a typed failure when the CA fetch is unavailable", async () => {
    const client = new WorkloadLeafClient(new FakeWorkloadCertificateIssuer());

    const result = await client.getCaTrustBundle();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
  });

  it("maps a success-without-data CA envelope to a typed failure (never an empty trust store)", async () => {
    const issuer = new FakeWorkloadCertificateIssuer({
      getCa: async () => ok(undefined as never),
    });
    const client = new WorkloadLeafClient(issuer);

    const result = await client.getCaTrustBundle();

    expect(result.failed).toBe(true);
    expect(result.statusCode).toBe(503);
  });

  it("assembles mutual-TLS channel credentials from the live leaf + trust bundle", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const credentials = await client.currentChannelCredentials();

    expect(credentials.success).toBe(true);
    expect(credentials.data!._isSecure()).toBe(true);
  });

  it("credential assembly bubbles the leaf failure when no leaf is available", async () => {
    const client = new WorkloadLeafClient(new FakeWorkloadCertificateIssuer());

    const credentials = await client.currentChannelCredentials();

    expect(credentials.failed).toBe(true);
    expect(credentials.statusCode).toBe(503);
  });

  it("credential assembly bubbles the CA failure when the chain fetch is down", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (csrDer) =>
        ok(await ca.issueLeafFromCsr(csrDer, new Date(Date.now() + HOUR_MS))),
      // no getCa script → unavailable
    });
    const client = new WorkloadLeafClient(issuer);

    const credentials = await client.currentChannelCredentials();

    expect(credentials.failed).toBe(true);
    expect(credentials.statusCode).toBe(503);
  });
});

describe("WorkloadLeafClient — cancellation + disposal", () => {
  it("maps an aborted issuance to a canceled result", async () => {
    const controller = new AbortController();
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (_csr, signal) => {
        controller.abort();
        throw new Error("aborted", { cause: signal });
      },
    });
    const client = new WorkloadLeafClient(issuer);

    const result = await client.getCurrentLeaf(controller.signal);

    expect(result.failed).toBe(true);
    expect(result.errorCode).toBe("CANCELED");
  });

  it("throws after dispose (fail-loud, matching the .NET ObjectDisposedException posture)", async () => {
    const client = new WorkloadLeafClient(new FakeWorkloadCertificateIssuer());
    client.dispose();

    await expect(client.getCurrentLeaf()).rejects.toThrow(/disposed/);
    await expect(client.forceReissue()).rejects.toThrow(/disposed/);
    await expect(client.getCaTrustBundle()).rejects.toThrow(/disposed/);
  });
});

describe("WorkloadLeafClient — structural secret pins", () => {
  it("the issuance request payload contains ONLY the CSR — never private-key material", async () => {
    const ca = await TestCertificateAuthority.create();
    const issuer = caBackedIssuer(ca, () => new Date(Date.now() + HOUR_MS));
    const client = new WorkloadLeafClient(issuer);

    const leaf = await client.getCurrentLeaf();
    expect(leaf.success).toBe(true);

    // Everything that crossed the seam is a CSR (public by construction) whose
    // DER does not embed the private key: the CSR's public key is derivable, and
    // the private scalar never appears — proven by the CSR parsing as a PUBLIC
    // structure and the private PEM's base64 not occurring in the request bytes.
    expect(issuer.csrsSeen).toHaveLength(1);
    const requestB64 = Buffer.from(issuer.csrsSeen[0]!).toString("base64");
    const privateB64 = leaf
      .data!.privateKeyPem.split("\n")
      .filter((l) => !l.startsWith("-----"))
      .join("");
    expect(requestB64).not.toContain(privateB64);
    expect(privateB64).not.toContain(requestB64);
  });

  it("no log capture contains key material on any path", async () => {
    const ca = await TestCertificateAuthority.create();
    const logger = new CapturingLogger();
    let now = 1_000_000;
    let mode: "ok" | "mismatch" | "garbage" = "ok";
    const issuer = new FakeWorkloadCertificateIssuer({
      issue: async (csrDer) => {
        const notAfter = new Date(now + 10 * 60 * 1000);
        if (mode === "garbage")
          return ok({
            certificateDer: new Uint8Array([0xba, 0xad]),
            issuerCertificateDer: new Uint8Array([0xf0, 0x0d]),
            notAfter: Temporal.Instant.fromEpochMilliseconds(
              notAfter.getTime(),
            ),
          });
        if (mode === "mismatch")
          return ok(await ca.issueLeafOverForeignKey(notAfter));
        return ok(await ca.issueLeafFromCsr(csrDer, notAfter));
      },
      getCa: async () => ok(ca.caChain()),
    });
    const client = new WorkloadLeafClient(issuer, {
      logger,
      now: () => now,
      refreshMarginMs: 5 * 60 * 1000,
      circuitFailureThreshold: 100,
    });

    const leaf = await client.getCurrentLeaf();
    expect(leaf.success).toBe(true);

    // Enter the refresh margin so each forceReissue genuinely reaches the issuer:
    // first the mismatch-reject log path, then the sanitized parse-failure path.
    now += 6 * 60 * 1000;
    mode = "mismatch";
    await client.forceReissue();
    expect(issuer.issueCallCount).toBe(2);

    mode = "garbage";
    await client.forceReissue();
    expect(issuer.issueCallCount).toBe(3);

    const logged = logger.allText();
    expect(logged).not.toContain("PRIVATE KEY");
    const privateB64 = leaf
      .data!.privateKeyPem.split("\n")
      .filter((l) => !l.startsWith("-----"))
      .join("");
    // No fragment of the private key's base64 appears in any log line.
    expect(logged).not.toContain(privateB64.slice(0, 24));
  });
});

function toArrayBuffer(u: Uint8Array): ArrayBuffer {
  const b = new ArrayBuffer(u.byteLength);
  new Uint8Array(b).set(u);
  return b;
}
