// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { IPayloadCrypto, IPayloadOpener } from "@d2/encryption";
import { EncryptionDomainModes } from "@d2/encryption-abstractions";
import { DlqFailureCauses } from "@d2/messaging-abstractions";

import type { BodyOpener } from "./body-opener.js";
import { MessageBodyDecodeError } from "./message-body-decode-error.js";

const _decoder = new TextDecoder("utf-8");
const _modeOf = EncryptionDomainModes as Readonly<
  Record<string, "sealed" | "symmetric" | undefined>
>;

/**
 * A mode-aware decrypting {@link BodyOpener} — the consumer twin of the .NET
 * `EncryptedBodyComposer.Decompose` sealed/symmetric branch, and the real
 * crypto that plugs into the body-decompose seam (the concrete decrypting
 * opener the seam previously stubbed). Built for one domain with one port:
 *
 * - a SEALED domain accepts ONLY v2 frames (the {@link IPayloadOpener} rejects
 *   any other version); a SYMMETRIC domain accepts ONLY v1 (the
 *   {@link IPayloadCrypto} rejects any other version);
 * - a plaintext body on an encrypted domain, tampering, an unknown kid, or the
 *   wrong frame version all surface as {@link MessageBodyDecodeError} with
 *   `DECRYPT_FAILURE` → the delivery routes to the DLQ (never a silent
 *   mis-decode); a decrypted-but-non-JSON body surfaces as `DESERIALIZE_FAILURE`.
 *
 * Deps: `@d2/encryption` only. The KC-backed port instance is composed by the
 * host — this shared lib never depends on a service-owned package.
 */
export class CryptoBodyOpener implements BodyOpener {
  readonly #domain: string;
  readonly #decrypt: (body: Uint8Array) => Promise<Uint8Array>;

  private constructor(
    domain: string,
    decrypt: (body: Uint8Array) => Promise<Uint8Array>,
  ) {
    this.#domain = domain;
    this.#decrypt = decrypt;
  }

  /**
   * Builds a sealed-domain opener over a private-keyring {@link IPayloadOpener}.
   *
   * @param domain The sealed encryption domain this opener serves.
   * @param opener The sealed payload opener (host-composed, KC-backed).
   */
  static sealed(domain: string, opener: IPayloadOpener): CryptoBodyOpener {
    return new CryptoBodyOpener(domain, (body) => opener.open(body));
  }

  /**
   * Builds a symmetric-domain opener over a keyring {@link IPayloadCrypto}.
   *
   * @param domain The symmetric encryption domain this opener serves.
   * @param crypto The symmetric payload crypto (host-composed, KC-backed).
   */
  static symmetric(domain: string, crypto: IPayloadCrypto): CryptoBodyOpener {
    return new CryptoBodyOpener(domain, (body) => crypto.decrypt(body));
  }

  /** The encryption domain this opener is bound to. */
  get domain(): string {
    return this.#domain;
  }

  /** @inheritdoc */
  async open(body: Buffer): Promise<unknown> {
    let plaintext: Uint8Array;

    try {
      plaintext = await this.#decrypt(body);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DECRYPT_FAILURE,
        `encrypted body for domain '${this.#domain}' could not be opened`,
        err,
      );
    }

    const text = _decoder.decode(plaintext);

    try {
      return JSON.parse(text);
    } catch (err) {
      throw new MessageBodyDecodeError(
        DlqFailureCauses.DESERIALIZE_FAILURE,
        `decrypted body for domain '${this.#domain}' is not valid UTF-8 JSON`,
        err,
      );
    }
  }
}

/**
 * Asserts a body opener matches an encryption domain's mode — the consumer-side
 * runtime cross-check (the TS twin of the .NET subscriber-vs-opener boot check):
 * a subscriber for a SEALED domain must be wired with a sealed
 * {@link CryptoBodyOpener} for that domain, never a plaintext or symmetric one.
 * Called by the host when composing a subscription; fails loud at wire-up.
 *
 * @param domain The message's encryption domain (from its descriptor).
 * @param opener The body opener the host wired for the subscription.
 * @throws When the opener does not match the domain's required mode.
 */
export function assertOpenerMatchesDomain(
  domain: string,
  opener: BodyOpener,
): void {
  const mode = _modeOf[domain];

  // Plaintext / symmetric domains do not require a CryptoBodyOpener here; the
  // check exists to catch a SEALED domain wired without its decrypting opener
  // (the forgotten-opener case that would otherwise DLQ every delivery).
  if (mode !== "sealed") {
    return;
  }

  if (!(opener instanceof CryptoBodyOpener) || opener.domain !== domain) {
    throw new Error(
      `Sealed domain '${domain}' requires a sealed CryptoBodyOpener for that ` +
        "domain, but none was wired — every delivery would DLQ. Wire the " +
        "KC-backed sealed opener for this consumer.",
    );
  }
}
