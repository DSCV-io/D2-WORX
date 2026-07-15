// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  RecipientPrivateKeyring,
  RecipientPublicKeyring,
} from "@d2/encryption";
import { type D2Result, bubbleFail, ok, serviceUnavailable } from "@d2/result";

import type { KeyCustodianGrpcClient } from "../facade/key-custodian-grpc-client.g.js";
import type { SealPrivateEntry } from "./get-or-lazy-provision-own-seal-private-key-dto.g.js";
import type { SealPublicEntry } from "./get-or-lazy-provision-seal-public-key-dto.g.js";

/**
 * Per-call gRPC deadline (ms) applied to every seal-keyring fetch — the TS twin of
 * the .NET `GrpcSealingClient`'s 10s per-call deadline (`GrpcSealingClient.cs:37`).
 * A connected-but-unresponsive KeyCustodian must never hang a fetch (opener boot,
 * lazy first-seal, or rotation-refresh) forever; without this the emitted client
 * issues the grpc-js call with no `CallOptions.deadline`.
 */
const _SEAL_CALL_DEADLINE_MS = 10_000;

/**
 * Least-privilege port over the two KeyCustodian seal ops — the TS twin of the
 * .NET `ISealingClient`. Maps the emitted wire DTOs to validated
 * `@d2/encryption` keyrings (P-256 import validated at construction).
 */
export interface SealingClient {
  /**
   * Fetches THIS service's private sealing keyring (targetless — identity is the
   * mutual-TLS caller). Mirrors `getOrLazyProvisionOwnSealPrivateKey`.
   *
   * @param signal Optional cancellation signal.
   */
  getOwnPrivateKeyring(
    signal?: AbortSignal,
  ): Promise<D2Result<RecipientPrivateKeyring>>;

  /**
   * Fetches a recipient service's public sealing keyring. Mirrors
   * `getOrLazyProvisionSealPublicKey`.
   *
   * @param recipientServiceId The recipient service to seal to.
   * @param signal Optional cancellation signal.
   */
  getPublicKeyring(
    recipientServiceId: string,
    signal?: AbortSignal,
  ): Promise<D2Result<RecipientPublicKeyring>>;
}

/**
 * The gRPC-backed {@link SealingClient} over the emitted
 * {@link KeyCustodianGrpcClient} facade (dialed over the mTLS channel by
 * the host). Output mappers build the validated recipient keyrings; the private
 * mapper carries the raw PKCS#8 bytes into a keyring that zeroizes on dispose.
 */
export class GrpcSealingClient implements SealingClient {
  readonly #client: KeyCustodianGrpcClient;
  readonly #ownServiceId: string;

  /**
   * @param client The emitted gRPC client facade.
   * @param ownServiceId This service's id (anchors the private-keyring AEAD).
   */
  constructor(client: KeyCustodianGrpcClient, ownServiceId: string) {
    this.#client = client;
    this.#ownServiceId = ownServiceId;
  }

  /** @inheritdoc */
  async getOwnPrivateKeyring(
    signal?: AbortSignal,
  ): Promise<D2Result<RecipientPrivateKeyring>> {
    const result = await this.#client.getOrLazyProvisionOwnSealPrivateKey(
      {},
      { deadlineMs: _SEAL_CALL_DEADLINE_MS, signal },
    );

    if (result.failed) {
      return bubbleFail(result);
    }

    if (result.data === undefined) {
      return serviceUnavailable();
    }

    const keyring = await RecipientPrivateKeyring.create(
      this.#ownServiceId,
      new Map(
        result.data.entries.map((e: SealPrivateEntry) => [
          e.kid,
          e.privatePkcs8,
        ]),
      ),
    );

    return ok(keyring);
  }

  /** @inheritdoc */
  async getPublicKeyring(
    recipientServiceId: string,
    signal?: AbortSignal,
  ): Promise<D2Result<RecipientPublicKeyring>> {
    const result = await this.#client.getOrLazyProvisionSealPublicKey(
      { serviceId: recipientServiceId },
      { deadlineMs: _SEAL_CALL_DEADLINE_MS, signal },
    );

    if (result.failed) {
      return bubbleFail(result);
    }

    if (result.data === undefined) {
      return serviceUnavailable();
    }

    const keyring = await RecipientPublicKeyring.create(
      recipientServiceId,
      result.data.activeKid,
      new Map(
        result.data.entries.map((e: SealPublicEntry) => [e.kid, e.publicSpki]),
      ),
    );

    return ok(keyring);
  }
}
