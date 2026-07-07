// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import { PayloadCryptoKeyring } from "@d2/encryption";
import type { ILogger } from "@d2/logging";
import {
  type D2Result,
  ok,
  serviceUnavailable,
  validationFailed,
} from "@d2/result";
import { describe, expect, it, vi } from "vitest";

import type { KeyCustodianGrpcClient } from "../src/facade/key-custodian-grpc-client.g.js";
import { KeyringBackedPayloadCrypto } from "../src/keyring/keyring-backed-payload-crypto.js";
import {
  GrpcKeyringClient,
  type KeyringClient,
} from "../src/keyring/keyring-client.js";

const utf8 = new TextEncoder();
const KID = "key-1";
const AAD = utf8.encode("d2/audit");

/** A test logger (§7.23 test-only). */
class FakeLogger implements ILogger {
  readonly messages: string[] = [];
  private rec(m: string): void {
    this.messages.push(m);
  }
  trace(m: string): void {
    this.rec(m);
  }
  debug(m: string): void {
    this.rec(m);
  }
  info(m: string): void {
    this.rec(m);
  }
  warn(m: string): void {
    this.rec(m);
  }
  error(m: string): void {
    this.rec(m);
  }
  fatal(m: string): void {
    this.rec(m);
  }
  child(): ILogger {
    return this;
  }
  logged(fragment: string): boolean {
    return this.messages.some((m) => m.includes(fragment));
  }
}

function keyring(): PayloadCryptoKeyring {
  const key = webcrypto.getRandomValues(new Uint8Array(32));
  return new PayloadCryptoKeyring(KID, new Map([[KID, key]]), AAD);
}

/** A configurable fake {@link KeyringClient} (§7.23 test-only). */
class FakeKeyringClient implements KeyringClient {
  calls = 0;
  fail = false;
  readonly #ring = keyring();
  async getKeyring(): Promise<D2Result<PayloadCryptoKeyring>> {
    this.calls++;
    return this.fail ? serviceUnavailable() : ok(this.#ring);
  }
}

const flush = (): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, 5));

describe("GrpcKeyringClient", () => {
  function facade(o: Partial<KeyCustodianGrpcClient>): KeyCustodianGrpcClient {
    return o as KeyCustodianGrpcClient;
  }

  it("maps a keyring response and threads the signal", async () => {
    const key = webcrypto.getRandomValues(new Uint8Array(32));
    let sawSignal = false;
    const client = new GrpcKeyringClient(
      facade({
        getKeyring: async (input, opts) => {
          expect(input.keyDomain).toBe("audit");
          sawSignal = opts?.signal !== undefined;
          return ok({
            activeKid: KID,
            entries: [{ kid: KID, keyBytes: key }],
            aadContext: AAD,
          });
        },
      }),
    );
    const result = await client.getKeyring(
      "audit",
      new AbortController().signal,
    );
    expect(result.data?.activeKid).toBe(KID);
    expect(sawSignal).toBe(true);
  });

  it("bounds every fetch with the 10s per-call gRPC deadline (.NET parity)", async () => {
    const key = webcrypto.getRandomValues(new Uint8Array(32));
    let seenDeadline: number | undefined;
    const client = new GrpcKeyringClient(
      facade({
        getKeyring: async (_input, opts) => {
          seenDeadline = opts?.deadlineMs;
          return ok({
            activeKid: KID,
            entries: [{ kid: KID, keyBytes: key }],
            aadContext: AAD,
          });
        },
      }),
    );
    await client.getKeyring("audit");
    // A connected-but-unresponsive KC must never hang a fetch (GrpcKeyringClient.cs:33).
    expect(seenDeadline).toBe(10_000);
  });

  it("bubbles a failure and maps missing data", async () => {
    const failing = new GrpcKeyringClient(
      facade({ getKeyring: async () => serviceUnavailable() }),
    );
    expect((await failing.getKeyring("audit")).failed).toBe(true);

    const empty = new GrpcKeyringClient(
      facade({ getKeyring: async () => ok(undefined) }),
    );
    expect((await empty.getKeyring("audit")).failed).toBe(true);
  });
});

describe("KeyringBackedPayloadCrypto", () => {
  it("boot-fetches, round-trips, hot-swaps, and grace-disposes", async () => {
    const client = new FakeKeyringClient();
    const crypto = await KeyringBackedPayloadCrypto.create(client, "audit", {
      logger: new FakeLogger(),
      graceMs: 0,
    });

    const frame = await crypto.encrypt(utf8.encode("secret"));
    expect(new Uint8Array(await crypto.decrypt(frame))).toEqual(
      utf8.encode("secret"),
    );

    await crypto.refresh();
    await flush(); // grace timer fires (graceMs = 0)

    crypto.dispose();
    crypto.dispose(); // idempotent
    expect(() => crypto.encrypt(utf8.encode("x"))).toThrow(/disposed/);
    expect(() => crypto.decrypt(frame)).toThrow(/disposed/);
    await expect(crypto.refresh()).rejects.toThrow(/disposed/);
  });

  it("fails loud when the boot fetch fails", async () => {
    const client = new FakeKeyringClient();
    client.fail = true;
    await expect(
      KeyringBackedPayloadCrypto.create(client, "audit", {
        logger: new FakeLogger(),
      }),
    ).rejects.toThrow(/fail-closed/);
  });

  it("serves the current keyring when refresh is exhausted, and cancels timers", async () => {
    const client = new FakeKeyringClient();
    const logger = new FakeLogger();
    const crypto = await KeyringBackedPayloadCrypto.create(client, "audit", {
      logger,
      refreshAttempts: 2,
      refreshBaseDelayMs: 1, // tiny backoff keeps the retry loop instant in tests
      // graceMs omitted — exercises the default (a long, unref'd grace timer).
    });
    await crypto.refresh(); // schedules the default-length grace timer (success swap)
    client.fail = true;
    await crypto.refresh(); // exhausts (transient 503), serves current
    expect(logger.logged("refresh exhausted")).toBe(true);

    crypto.dispose(); // clears the pending grace timer
  });

  it("short-circuits refresh on a permanent (non-transient) failure — no retry", async () => {
    let calls = 0;
    const ring = keyring();
    const client: KeyringClient = {
      getKeyring: async () => {
        calls++;

        return calls === 1
          ? ok(ring)
          : validationFailed<PayloadCryptoKeyring>();
      },
    };
    const logger = new FakeLogger();
    const crypto = await KeyringBackedPayloadCrypto.create(client, "audit", {
      logger,
      refreshAttempts: 3,
      refreshBaseDelayMs: 1,
    });

    expect(calls).toBe(1); // boot fetch

    await crypto.refresh();

    // A permanent 400 is NOT retried (mirrors the .NET RetryD2ResultAsync default):
    // exactly ONE refresh fetch, not the full attempt budget.
    expect(calls).toBe(2);
    expect(logger.logged("refresh exhausted")).toBe(true);

    crypto.dispose();
  });

  it("force-zeroizes a displaced keyring on dispose (not just the timer)", async () => {
    const ringA = keyring();
    const ringB = keyring();
    let calls = 0;
    const client: KeyringClient = {
      getKeyring: async () => {
        calls++;

        return ok(calls === 1 ? ringA : ringB);
      },
    };
    const crypto = await KeyringBackedPayloadCrypto.create(client, "audit", {
      logger: new FakeLogger(),
      graceMs: 100_000, // long grace: the displaced ring is NOT zeroized before dispose
    });
    const disposeA = vi.spyOn(ringA, "dispose");
    const disposeB = vi.spyOn(ringB, "dispose");

    await crypto.refresh(); // swaps to ringB; ringA displaced under the long grace
    expect(disposeA).not.toHaveBeenCalled();

    crypto.dispose();

    // The displaced ringA must be zeroized on dispose — clearing its grace timer
    // alone would skip the zeroize its closure performs (leaking key material).
    expect(disposeA).toHaveBeenCalled();
    expect(disposeB).toHaveBeenCalled();
  });
});
