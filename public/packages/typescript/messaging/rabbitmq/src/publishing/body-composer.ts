// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IPayloadCrypto, IPayloadSealer } from "@dcsv-io/d2-encryption";
import { EncryptionDomainModes } from "@dcsv-io/d2-encryption-abstractions";
import type { MqMessageDescriptor } from "@dcsv-io/d2-messaging-abstractions";

import { readEncryptionKid } from "./encryption-kid.js";

/** The `plaintext` sentinel domain — ships cleartext, needs no composer. */
const _PLAINTEXT = "plaintext";

/** A wired composer for one domain — a sealer (sealed) or crypto (symmetric). */
export type Composer = IPayloadSealer | IPayloadCrypto;

/** The composed AMQP body plus the frame kid (absent for plaintext). */
export interface ComposedBody {
  readonly body: Uint8Array;
  readonly kid?: string;
}

// A lookup with a coercible index — `EncryptionDomainModes` is `as const`, so a
// non-key index would be a compile error; this cast lets the RUNTIME default-deny
// (unknown domain → undefined → throw) work for dynamic / fixture descriptors.
const _modeOf = EncryptionDomainModes as Readonly<
  Record<string, "sealed" | "symmetric" | undefined>
>;

/**
 * Composes the AMQP body for a message — the runtime half of the publish/encrypt
 * fusion (the second lock beneath the compile-time type witness). The descriptor's
 * encryption domain mode is consulted UNCONDITIONALLY: `plaintext` → JSON as-is;
 * `sealed` → the domain's sealer; `symmetric` → the domain's crypto. This is
 * default-deny — a missing composer for an encrypted domain, or an unknown
 * domain, fails loud BEFORE any socket write. There is no code path that emits a
 * cleartext body for an encrypted domain.
 *
 * @param descriptor The resolved message descriptor.
 * @param json The serialized message bytes.
 * @param composers The wired composers, keyed by domain.
 * @returns The composed body + frame kid.
 * @throws When an encrypted domain has no wired composer or an unknown mode.
 */
export async function composeBody(
  descriptor: MqMessageDescriptor,
  json: Uint8Array,
  composers: Readonly<Record<string, Composer | undefined>>,
  modes: Readonly<Record<string, "sealed" | "symmetric" | undefined>> = _modeOf,
): Promise<ComposedBody> {
  const domain = descriptor.encryption;

  if (domain === _PLAINTEXT) {
    return { body: json, kid: undefined };
  }

  const mode = modes[domain];

  if (mode === undefined) {
    throw new Error(
      `Message domain '${domain}' is not a known encryption domain — refusing ` +
        "to publish (default-deny).",
    );
  }

  const composer = composers[domain];

  if (composer === undefined) {
    throw new Error(
      `No composer wired for encrypted domain '${domain}' — refusing to publish ` +
        "(default-deny).",
    );
  }

  const body =
    mode === "sealed"
      ? await (composer as IPayloadSealer).seal(json)
      : await (composer as IPayloadCrypto).encrypt(json);

  return { body, kid: readEncryptionKid(body) };
}
