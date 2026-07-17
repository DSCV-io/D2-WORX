// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { webcrypto } from "node:crypto";

import {
  RabbitMQContainer,
  type StartedRabbitMQContainer,
} from "@testcontainers/rabbitmq";
import {
  PayloadCrypto,
  PayloadCryptoKeyring,
  PayloadOpener,
  PayloadSealer,
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@dcsv-io/d2-encryption";
import type { ILogger } from "@dcsv-io/d2-logging";
import type { MqMessageDescriptor } from "@dcsv-io/d2-messaging-abstractions";
import { ok } from "@dcsv-io/d2-result";
import { uuidv7 } from "@dcsv-io/d2-utilities";
import { Connection } from "rabbitmq-client";
import { afterAll, beforeAll, describe, expect, it } from "vitest";

import {
  CryptoBodyOpener,
  QueuePattern,
  type SubscriptionDescriptor,
  composeBody,
  createPublisher,
  subscribe,
} from "../../src/index.js";

// -----------------------------------------------------------------------
// The shipped TS consumer pipeline decrypting a REAL encrypted frame off a REAL
// broker. Unit round-trips (crypto-body-opener.test.ts / publisher.test.ts) and
// the cross-runtime golden/KAT gates cover the codecs; these two prove the full
// delivery leg — publish (shipped compose / createPublisher fusion) → live
// RabbitMQ → shipped `subscribe` + `CryptoBodyOpener` decrypt → handler sees
// the plaintext object.
// -----------------------------------------------------------------------

const utf8 = new TextEncoder();

// Attempt-budget stuck-guard — the TS twin of the .NET `_POLL_ATTEMPT_BUDGET`
// (DlqTests.cs / PublishConsumeRoundTripTests.cs). Each iteration awaits a fixed
// poll interval; the budget caps ATTEMPTS, never elapsed wall-clock time, so
// under load the effective wait GROWS with the slowdown instead of a
// `Date.now()` deadline expiring mid-progress. This is NOT a success-path
// deadline: healthy delivery resolves in a handful of attempts; the budget only
// terminates a permanently-stuck test. (The 120s vitest `testTimeout` is the
// outer framework net, not part of any assertion's success path.)
const _POLL_INTERVAL_MS = 100;
const _POLL_ATTEMPT_BUDGET = 600;

async function waitUntil(predicate: () => boolean): Promise<void> {
  for (let attempt = 0; attempt < _POLL_ATTEMPT_BUDGET; attempt++) {
    if (predicate()) return;
    await new Promise((r) => setTimeout(r, _POLL_INTERVAL_MS));
  }

  throw new Error(
    `predicate not satisfied within ${_POLL_ATTEMPT_BUDGET} poll attempts`,
  );
}

let uniqueCounter = 0;
function unique(prefix: string): string {
  uniqueCounter += 1;
  return `${prefix}.${Date.now().toString(36)}.${uniqueCounter}`;
}

function makeSubscription(
  queueName: string,
  exchange: string,
): SubscriptionDescriptor {
  return {
    queueName,
    exchange,
    exchangeType: "fanout",
    pattern: QueuePattern.DurableShared,
    routingKeyBinding: "",
    prefetch: 4,
    idempotency: false,
    nackedBy: "test-consumer",
  };
}

async function makeSealerOpener(
  serviceId: string,
  kid: string,
): Promise<{ sealer: PayloadSealer; opener: PayloadOpener }> {
  const pair = await webcrypto.subtle.generateKey(
    { name: "ECDH", namedCurve: "P-256" },
    true,
    ["deriveBits"],
  );
  const spki = new Uint8Array(
    await webcrypto.subtle.exportKey("spki", pair.publicKey),
  );
  const pkcs8 = new Uint8Array(
    await webcrypto.subtle.exportKey("pkcs8", pair.privateKey),
  );
  const pub = await RecipientPublicKeyring.create(
    serviceId,
    kid,
    new Map([[kid, spki]]),
  );
  const priv = await RecipientPrivateKeyring.create(
    serviceId,
    new Map([[kid, pkcs8]]),
  );

  return { sealer: new PayloadSealer(pub), opener: new PayloadOpener(priv) };
}

describe("@dcsv-io/d2-messaging-rabbitmq consumer — real-broker encrypted decrypt", () => {
  let container: StartedRabbitMQContainer;
  let connection: Connection;

  beforeAll(async () => {
    container = await new RabbitMQContainer("rabbitmq:3.13-management").start();
    // getAmqpUrl() omits credentials; the container's default user is guest/guest.
    const url = container
      .getAmqpUrl()
      .replace("amqp://", "amqp://guest:guest@");
    connection = new Connection(url);
    await connection.onConnect();
  }, 180_000);

  afterAll(async () => {
    await connection?.close();
    await container?.stop();
  });

  async function publishRaw(
    exchange: string,
    body: Uint8Array,
    messageId: string,
  ): Promise<void> {
    const publisher = connection.createPublisher({ confirm: true });
    try {
      await publisher.send(
        {
          exchange,
          routingKey: "",
          durable: true,
          contentType: "application/octet-stream",
          messageId,
        },
        Buffer.from(body),
      );
    } finally {
      await publisher.close();
    }
  }

  it("row 1 — symmetric: consumes + decrypts a real v1 frame off the broker via CryptoBodyOpener.symmetric", async () => {
    // §7.23 fixture marker in the domain leaf. The production catalog carries no
    // symmetric domain, so this is a test-seam domain (Symmetric by default).
    const domain = "payload-fixture-symmetric";
    const kid = "sym-fixture-1";
    const crypto = new PayloadCrypto(
      new PayloadCryptoKeyring(
        kid,
        new Map([[kid, new Uint8Array(32).fill(11)]]),
        utf8.encode("d2/" + domain),
      ),
    );
    const exchange = unique("d2.test.sym");
    const queueName = unique("q.sym");
    const payload = { marker: "sym-" + uuidv7(), n: 42 };

    // Publish side — the shipped symmetric compose arm builds the real v1 frame.
    // createPublisher/publishVia resolve the mode from the production catalog
    // (which has no symmetric domain), so the frame is composed via
    // `composeBody`'s mode-override seam (the same seam publisher.test.ts uses)
    // and put on the wire by a raw confirm-publisher.
    const descriptor: MqMessageDescriptor = {
      constant: "SymFixture",
      messageType: "D2.Test.SymFixture",
      exchange,
      exchangeType: "fanout",
      encryption: domain,
    };
    const composed = await composeBody(
      descriptor,
      utf8.encode(JSON.stringify(payload)),
      { [domain]: crypto },
      { [domain]: "symmetric" },
    );
    expect(composed.body[0]).toBe(1); // v1 symmetric frame version byte

    // Consume side — the shipped subscriber pipeline + the real decrypting opener.
    let received: unknown;
    const sub = subscribe({
      connection,
      descriptor: makeSubscription(queueName, exchange),
      logger: silentLogger(),
      opener: CryptoBodyOpener.symmetric(domain, crypto),
      handler: (message) => {
        received = message;
        return ok();
      },
    });
    await sub.ready;

    await publishRaw(exchange, composed.body, uuidv7());
    await waitUntil(() => received !== undefined);

    expect(received).toEqual(payload);
    await sub.close();
  });

  it("row 2 — sealed: consumes + opens a real v2 sealed frame off the broker via CryptoBodyOpener.sealed", async () => {
    // Public catalog sealed domain (`payload-fixture-sealed`) — seals to the
    // recipient public keyring; only the private-keyring holder opens.
    const domain = "payload-fixture-sealed";
    const kid = "seal-fixture-1";
    const { sealer, opener } = await makeSealerOpener(domain, kid);
    const exchange = unique("d2.test.sealed");
    const queueName = unique("q.sealed");
    const payload = { marker: "sealed-" + uuidv7(), secret: "top" };

    // Publish side — the shipped createPublisher fusion over the live connection.
    // The production catalog carries no sealed MESSAGE, so the documented
    // `descriptors` fixture-registry option supplies one; the publish key is cast
    // because the compile-time type-witness is scoped to the production catalog
    // (fixture messages intentionally live outside it — runtime default-deny +
    // the injected registry are the enforcement here).
    // crypto map key MUST equal descriptor.encryption (composeBody domain lookup).
    const fixtureRegistry: Readonly<Record<string, MqMessageDescriptor>> = {
      SealedAuditFixture: {
        constant: "SealedAuditFixture",
        messageType: "D2.Test.SealedAuditFixture",
        exchange,
        exchangeType: "fanout",
        encryption: domain,
      },
    };
    const publisher = createPublisher(connection, {
      crypto: { "payload-fixture-sealed": sealer },
      logger: silentLogger(),
      descriptors: fixtureRegistry,
    });

    // Consume side — the shipped subscriber pipeline + the real sealed opener.
    let received: unknown;
    const sub = subscribe({
      connection,
      descriptor: makeSubscription(queueName, exchange),
      logger: silentLogger(),
      opener: CryptoBodyOpener.sealed(domain, opener),
      handler: (message) => {
        received = message;
        return ok();
      },
    });
    await sub.ready;

    const publish = await (
      publisher.publish as (
        key: string,
        message: object,
      ) => Promise<{ failed: boolean }>
    )("SealedAuditFixture", payload);
    expect(publish.failed).toBe(false);

    await waitUntil(() => received !== undefined);
    expect(received).toEqual(payload);

    await publisher.close();
    await sub.close();
  });
});

function silentLogger(): ILogger {
  const noop = (): void => undefined;
  const logger: ILogger = {
    trace: noop,
    debug: noop,
    info: noop,
    warn: noop,
    error: noop,
    fatal: noop,
    child: () => logger,
  };
  return logger;
}
