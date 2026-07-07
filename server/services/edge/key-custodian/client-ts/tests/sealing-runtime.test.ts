// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@d2/encryption";
import type { ILogger, LogBindings } from "@d2/logging";
import {
  type D2Result,
  ok,
  serviceUnavailable,
  validationFailed,
} from "@d2/result";
import { describe, expect, it, vi } from "vitest";

import type { KeyCustodianGrpcClient } from "../src/facade/key-custodian-grpc-client.g.js";
import type { RotationSubscription } from "../src/rotation/rotation-subscription.js";
import {
  createSealedCryptoViaKeyCustodian,
  type CreateSealedCryptoOptions,
} from "../src/sealing/create-sealed-crypto.js";
import { KeyringBackedPayloadOpener } from "../src/sealing/keyring-backed-payload-opener.js";
import { KeyringBackedPayloadSealer } from "../src/sealing/keyring-backed-payload-sealer.js";
import {
  GrpcSealingClient,
  type SealingClient,
} from "../src/sealing/sealing-client.js";

const utf8 = new TextEncoder();

/** A test logger that records levels + messages (§7.23 test-only). */
class FakeLogger implements ILogger {
  readonly messages: string[] = [];
  private rec(m: string): void {
    this.messages.push(m);
  }
  trace(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  debug(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  info(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  warn(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  error(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  fatal(m: string, _b?: LogBindings): void {
    this.rec(m);
  }
  child(): ILogger {
    return this;
  }
  logged(fragment: string): boolean {
    return this.messages.some((m) => m.includes(fragment));
  }
}

interface Keypair {
  readonly publicSpki: Uint8Array;
  readonly privatePkcs8: Uint8Array;
}

async function keypair(): Promise<Keypair> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  return {
    publicSpki: new Uint8Array(
      await webcrypto.subtle.exportKey("spki", pair.publicKey),
    ),
    privatePkcs8: new Uint8Array(
      await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
    ),
  };
}

/** A configurable fake {@link SealingClient} (§7.23 test-only). */
class FakeSealingClient implements SealingClient {
  privCalls = 0;
  pubCalls = 0;
  constructor(
    private readonly serviceId: string,
    private readonly kid: string,
    private readonly kp: Keypair,
    private failPrivate = false,
    private failPublic = false,
  ) {}
  async getOwnPrivateKeyring(): Promise<D2Result<RecipientPrivateKeyring>> {
    this.privCalls++;
    if (this.failPrivate) return serviceUnavailable();
    return ok(
      await RecipientPrivateKeyring.create(
        this.serviceId,
        new Map([[this.kid, this.kp.privatePkcs8]]),
      ),
    );
  }
  async getPublicKeyring(): Promise<D2Result<RecipientPublicKeyring>> {
    this.pubCalls++;
    if (this.failPublic) return serviceUnavailable();
    return ok(
      await RecipientPublicKeyring.create(
        this.serviceId,
        this.kid,
        new Map([[this.kid, this.kp.publicSpki]]),
      ),
    );
  }
}

const flush = (): Promise<void> =>
  new Promise((resolve) => setTimeout(resolve, 5));

/**
 * A no-op {@link RotationSubscription} (§7.23 test-only) — records nothing and never
 * fires. Used where a test does not exercise rotation but must still satisfy the now
 * MANDATORY `rotationSubscription` argument (the silent-stale-keys footgun is gone).
 */
const noopRotationFake: RotationSubscription = {
  onRotation: () => () => {},
};

describe("GrpcSealingClient", () => {
  function facade(
    overrides: Partial<KeyCustodianGrpcClient>,
  ): KeyCustodianGrpcClient {
    return overrides as KeyCustodianGrpcClient;
  }

  it("maps a private-keyring response and passes a signal through", async () => {
    const kp = await keypair();
    let sawSignal = false;
    const client = new GrpcSealingClient(
      facade({
        getOrLazyProvisionOwnSealPrivateKey: async (_i, opts) => {
          sawSignal = opts?.signal !== undefined;
          return ok({
            activeKid: "k1",
            entries: [{ kid: "k1", privatePkcs8: kp.privatePkcs8 }],
          });
        },
      }),
      "audit",
    );
    const result = await client.getOwnPrivateKeyring(
      new AbortController().signal,
    );
    expect(result.failed).toBe(false);
    expect(result.data?.recipientServiceId).toBe("audit");
    expect(sawSignal).toBe(true);
  });

  it("bounds every seal fetch with the 10s per-call gRPC deadline (.NET parity)", async () => {
    const kp = await keypair();
    let privateDeadline: number | undefined;
    let publicDeadline: number | undefined;
    const client = new GrpcSealingClient(
      facade({
        getOrLazyProvisionOwnSealPrivateKey: async (_i, opts) => {
          privateDeadline = opts?.deadlineMs;
          return ok({
            activeKid: "k1",
            entries: [{ kid: "k1", privatePkcs8: kp.privatePkcs8 }],
          });
        },
        getOrLazyProvisionSealPublicKey: async (_i, opts) => {
          publicDeadline = opts?.deadlineMs;
          return ok({
            activeKid: "k1",
            entries: [{ kid: "k1", publicSpki: kp.publicSpki }],
          });
        },
      }),
      "audit",
    );

    await client.getOwnPrivateKeyring();
    await client.getPublicKeyring("courier");

    // A connected-but-unresponsive KC must never hang a fetch (GrpcSealingClient.cs:37).
    expect(privateDeadline).toBe(10_000);
    expect(publicDeadline).toBe(10_000);
  });

  it("bubbles a failed private-keyring call and maps missing data", async () => {
    const failing = new GrpcSealingClient(
      facade({
        getOrLazyProvisionOwnSealPrivateKey: async () => serviceUnavailable(),
      }),
      "audit",
    );
    expect((await failing.getOwnPrivateKeyring()).failed).toBe(true);

    const empty = new GrpcSealingClient(
      facade({
        getOrLazyProvisionOwnSealPrivateKey: async () => ok(undefined),
      }),
      "audit",
    );
    expect((await empty.getOwnPrivateKeyring()).failed).toBe(true);
  });

  it("maps a public-keyring response, bubbles failure, and maps missing data", async () => {
    const kp = await keypair();
    let sawSignal = false;
    const client = new GrpcSealingClient(
      facade({
        getOrLazyProvisionSealPublicKey: async (input, opts) => {
          expect(input.serviceId).toBe("courier");
          sawSignal = opts?.signal !== undefined;
          return ok({
            activeKid: "k1",
            entries: [{ kid: "k1", publicSpki: kp.publicSpki }],
          });
        },
      }),
      "audit",
    );
    const result = await client.getPublicKeyring(
      "courier",
      new AbortController().signal,
    );
    expect(result.data?.activeKid).toBe("k1");
    expect(sawSignal).toBe(true);

    const failing = new GrpcSealingClient(
      facade({
        getOrLazyProvisionSealPublicKey: async () => serviceUnavailable(),
      }),
      "audit",
    );
    expect((await failing.getPublicKeyring("courier")).failed).toBe(true);

    const empty = new GrpcSealingClient(
      facade({ getOrLazyProvisionSealPublicKey: async () => ok(undefined) }),
      "audit",
    );
    expect((await empty.getPublicKeyring("courier")).failed).toBe(true);
  });
});

describe("KeyringBackedPayloadOpener", () => {
  it("boot-fetches, opens, hot-swaps, and grace-disposes", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const logger = new FakeLogger();
    const opener = await KeyringBackedPayloadOpener.create(client, {
      logger,
      graceMs: 0,
    });

    const sealer = new PayloadSealer(
      await RecipientPublicKeyring.create(
        "audit",
        "seal-1",
        new Map([["seal-1", kp.publicSpki]]),
      ),
    );
    const frame = await sealer.seal(utf8.encode(JSON.stringify({ x: 1 })));
    expect(new Uint8Array(await opener.open(frame))).toEqual(
      utf8.encode(JSON.stringify({ x: 1 })),
    );

    await opener.refresh();
    await flush(); // let the grace timer fire (graceMs = 0)
    expect(new Uint8Array(await opener.open(frame))).toEqual(
      utf8.encode(JSON.stringify({ x: 1 })),
    );

    opener.dispose();
    opener.dispose(); // idempotent
    expect(() => opener.open(frame)).toThrow(/disposed/);
    await expect(opener.refresh()).rejects.toThrow(/disposed/);
  });

  it("fails loud when the boot fetch fails", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp, true);
    await expect(
      KeyringBackedPayloadOpener.create(client, { logger: new FakeLogger() }),
    ).rejects.toThrow(/fail-closed/);
  });

  it("serves the current keyring when refresh is exhausted", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const logger = new FakeLogger();
    const opener = await KeyringBackedPayloadOpener.create(client, {
      logger,
      refreshAttempts: 2,
      refreshBaseDelayMs: 1, // tiny backoff keeps the retry loop instant in tests
    });
    // Flip the client to fail; refresh exhausts (transient 503) and serves current.
    (client as unknown as { failPrivate: boolean }).failPrivate = true;
    await opener.refresh();
    expect(logger.logged("refresh exhausted")).toBe(true);

    opener.dispose();
  });

  it("short-circuits refresh on a permanent (non-transient) failure — no retry", async () => {
    const kp = await keypair();
    let calls = 0;
    const client: SealingClient = {
      getOwnPrivateKeyring: async () => {
        calls++;

        return calls === 1
          ? ok(
              await RecipientPrivateKeyring.create(
                "audit",
                new Map([["seal-1", kp.privatePkcs8]]),
              ),
            )
          : validationFailed<RecipientPrivateKeyring>();
      },
      getPublicKeyring: async () => serviceUnavailable(),
    };
    const logger = new FakeLogger();
    const opener = await KeyringBackedPayloadOpener.create(client, {
      logger,
      refreshAttempts: 3,
      refreshBaseDelayMs: 1,
    });

    expect(calls).toBe(1); // boot fetch

    await opener.refresh();

    // A permanent 400 is NOT retried (mirrors the .NET RetryD2ResultAsync default).
    expect(calls).toBe(2);
    expect(logger.logged("refresh exhausted")).toBe(true);

    opener.dispose();
  });

  it("force-zeroizes a displaced private keyring on dispose (not just the timer)", async () => {
    const kpA = await keypair();
    const kpB = await keypair();
    const ringA = await RecipientPrivateKeyring.create(
      "audit",
      new Map([["seal-1", kpA.privatePkcs8]]),
    );
    const ringB = await RecipientPrivateKeyring.create(
      "audit",
      new Map([["seal-2", kpB.privatePkcs8]]),
    );
    let calls = 0;
    const client: SealingClient = {
      getOwnPrivateKeyring: async () => {
        calls++;

        return ok(calls === 1 ? ringA : ringB);
      },
      getPublicKeyring: async () => serviceUnavailable(),
    };
    const opener = await KeyringBackedPayloadOpener.create(client, {
      logger: new FakeLogger(),
      graceMs: 100_000, // long grace: ringA is NOT zeroized before dispose
    });
    const disposeA = vi.spyOn(ringA, "dispose");
    const disposeB = vi.spyOn(ringB, "dispose");

    await opener.refresh(); // swaps to ringB; ringA displaced under the long grace
    expect(disposeA).not.toHaveBeenCalled();

    opener.dispose();

    // The displaced private ringA must be zeroized on dispose — clearing its grace
    // timer alone would skip the zeroize its closure performs (leaking private keys).
    expect(disposeA).toHaveBeenCalled();
    expect(disposeB).toHaveBeenCalled();
  });

  it("cancels a pending grace timer on dispose", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const opener = await KeyringBackedPayloadOpener.create(client, {
      logger: new FakeLogger(),
      graceMs: 100_000,
    });
    await opener.refresh(); // schedules a long grace timer
    opener.dispose(); // clears the pending timer
  });
});

describe("KeyringBackedPayloadSealer", () => {
  it("lazily fetches on first seal, reuses, hot-swaps, and disposes", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const logger = new FakeLogger();
    const sealer = KeyringBackedPayloadSealer.create(client, "audit", {
      logger,
    });

    expect(client.pubCalls).toBe(0);
    const frame = await sealer.seal(utf8.encode("hi"));
    expect(client.pubCalls).toBe(1); // lazy fetch
    expect(frame[0]).toBe(2);
    await sealer.seal(utf8.encode("again"));
    expect(client.pubCalls).toBe(1); // reused, no refetch

    await sealer.refresh();
    expect(client.pubCalls).toBe(2); // refresh refetched

    sealer.dispose();
    await expect(sealer.seal(utf8.encode("x"))).rejects.toThrow(/disposed/);
    await expect(sealer.refresh()).rejects.toThrow(/disposed/);
  });

  it("fails loud (never plaintext) when the lazy fetch fails", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp, false, true);
    const sealer = KeyringBackedPayloadSealer.create(client, "audit", {
      logger: new FakeLogger(),
    });
    await expect(sealer.seal(utf8.encode("x"))).rejects.toThrow(/cannot seal/);
  });

  it("serves the current keyring when refresh is exhausted", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const logger = new FakeLogger();
    const sealer = KeyringBackedPayloadSealer.create(client, "audit", {
      logger,
      refreshAttempts: 2,
      refreshBaseDelayMs: 1, // tiny backoff keeps the retry loop instant in tests
    });
    await sealer.seal(utf8.encode("x"));
    (client as unknown as { failPublic: boolean }).failPublic = true;
    await sealer.refresh();
    expect(logger.logged("refresh exhausted")).toBe(true);
  });

  it("dedupes concurrent first-seals onto a single fetch", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const sealer = KeyringBackedPayloadSealer.create(client, "audit", {
      logger: new FakeLogger(),
    });

    // Two concurrent first-seals: the in-flight fetch is shared (the .NET twin
    // dedupes under r_initLock) — exactly one public-keyring fetch, not two.
    const [f1, f2] = await Promise.all([
      sealer.seal(utf8.encode("a")),
      sealer.seal(utf8.encode("b")),
    ]);

    expect(f1[0]).toBe(2);
    expect(f2[0]).toBe(2);
    expect(client.pubCalls).toBe(1);

    sealer.dispose();
  });

  it("short-circuits refresh on a permanent (non-transient) failure — no retry", async () => {
    const kp = await keypair();
    const pub = await RecipientPublicKeyring.create(
      "audit",
      "seal-1",
      new Map([["seal-1", kp.publicSpki]]),
    );
    let calls = 0;
    const client: SealingClient = {
      getOwnPrivateKeyring: async () => serviceUnavailable(),
      getPublicKeyring: async () => {
        calls++;

        return calls === 1
          ? ok(pub)
          : validationFailed<RecipientPublicKeyring>();
      },
    };
    const logger = new FakeLogger();
    const sealer = KeyringBackedPayloadSealer.create(client, "audit", {
      logger,
      refreshAttempts: 3,
      refreshBaseDelayMs: 1,
    });

    await sealer.seal(utf8.encode("x")); // lazy first fetch (calls = 1)

    await sealer.refresh();

    // A permanent 400 is NOT retried (mirrors the .NET RetryD2ResultAsync default).
    expect(calls).toBe(2);
    expect(logger.logged("refresh exhausted")).toBe(true);

    sealer.dispose();
  });
});

describe("createSealedCryptoViaKeyCustodian", () => {
  it("rejects an invalid ownServiceId", async () => {
    await expect(
      createSealedCryptoViaKeyCustodian({
        ownServiceId: "NOT VALID",
        sealingClient: new FakeSealingClient("audit", "k", await keypair()),
        logger: new FakeLogger(),
        rotationSubscription: noopRotationFake,
      }),
    ).rejects.toThrow(/service-id grammar/);
  });

  it("makes rotationSubscription REQUIRED at compile time (no silent-stale footgun)", () => {
    // Compile-time proof (§Gap-2): omitting `rotationSubscription` must no longer
    // type-check — a caller can never silently skip rotation wiring and serve stale
    // keys after a KeyCustodian rotation. Assigning an options object WITHOUT the
    // property to the options type is the type error `@ts-expect-error` pins.
    const optionsWithoutRotation = {
      ownServiceId: "audit",
      sealingClient: {} as SealingClient,
      logger: new FakeLogger(),
    };
    // @ts-expect-error — rotationSubscription is REQUIRED; omitting it must not type-check.
    const _proof: CreateSealedCryptoOptions = optionsWithoutRotation;

    expect(_proof).toBe(optionsWithoutRotation);
  });

  it("wires a sealer per consumer service and the opener when a consumer", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const registered: string[] = [];
    const wiring = await createSealedCryptoViaKeyCustodian({
      ownServiceId: "audit",
      sealingClient: client,
      logger: new FakeLogger(),
      rotationSubscription: {
        onRotation: (domain, handler) => {
          registered.push(domain);
          if (domain === "seal:audit") handler();
          return () => {
            registered.push(`off:${domain}`);
          };
        },
      },
    });

    expect([...wiring.sealersByConsumerService.keys()].sort()).toEqual([
      "audit",
      "courier",
      "notifications",
    ]);
    expect(wiring.ownOpener).toBeDefined(); // audit is a consumer
    expect(registered).toContain("seal:audit");
    expect(registered).toContain("seal:courier");
    await flush(); // let the invoked seal:audit refresh run

    wiring.dispose();
    expect(
      registered.filter((d) => d.startsWith("off:")).length,
    ).toBeGreaterThan(0);
  });

  it("builds and boot-fetches the opener for a consumer service", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("courier", "seal-1", kp);
    const wiring = await createSealedCryptoViaKeyCustodian({
      ownServiceId: "courier",
      sealingClient: client,
      logger: new FakeLogger(),
      rotationSubscription: noopRotationFake,
    });
    expect(wiring.ownOpener).toBeDefined(); // courier is a consumer
    expect(client.privCalls).toBe(1); // opener boot fetch
    wiring.dispose();
  });

  it("omits the opener for a non-consumer service", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    const wiring = await createSealedCryptoViaKeyCustodian({
      ownServiceId: "edge",
      sealingClient: client,
      logger: new FakeLogger(),
      rotationSubscription: noopRotationFake,
    });
    expect(wiring.ownOpener).toBeUndefined();
    expect(client.privCalls).toBe(0); // no boot fetch without an opener
    expect(wiring.sealersByConsumerService.size).toBe(3);
    wiring.dispose();
  });

  it("guards a double dispose() — the second call is a no-op (unsubscribes + disposes fire once)", async () => {
    const kp = await keypair();
    const client = new FakeSealingClient("audit", "seal-1", kp);
    let unsubscribeCalls = 0;
    const wiring = await createSealedCryptoViaKeyCustodian({
      ownServiceId: "audit",
      sealingClient: client,
      logger: new FakeLogger(),
      rotationSubscription: {
        onRotation: () => () => {
          unsubscribeCalls++;
        },
      },
    });
    const openerDispose = vi.spyOn(wiring.ownOpener!, "dispose");

    wiring.dispose();
    const afterFirst = unsubscribeCalls;
    wiring.dispose(); // re-entry must short-circuit

    // RotationSubscription does not guarantee an idempotent unsubscribe, so a second
    // dispose() must NOT re-fire any unsubscribe (nor re-dispose the opener) — without
    // the _disposed guard the second dispose() doubles both counts.
    expect(afterFirst).toBeGreaterThan(0); // real unsubscribes ran on the first dispose
    expect(unsubscribeCalls).toBe(afterFirst); // the second dispose added none
    expect(openerDispose).toHaveBeenCalledTimes(1);
  });
});
