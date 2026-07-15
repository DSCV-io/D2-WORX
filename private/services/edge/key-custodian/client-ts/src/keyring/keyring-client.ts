// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { PayloadCryptoKeyring } from "@d2/encryption";
import { type D2Result, bubbleFail, ok, serviceUnavailable } from "@d2/result";

import type { KeyCustodianGrpcClient } from "../facade/key-custodian-grpc-client.g.js";
import type { KeyringEntry } from "./get-keyring-dto.g.js";

/**
 * Per-call gRPC deadline (ms) applied to every keyring fetch — the TS twin of the
 * .NET `GrpcKeyringClient`'s 10s per-call deadline (`GrpcKeyringClient.cs:33`). A
 * connected-but-unresponsive KeyCustodian must never hang a fetch (boot,
 * lazy, or rotation-refresh) forever; without this the emitted client issues the
 * grpc-js call with no `CallOptions.deadline`.
 */
const _KEYRING_CALL_DEADLINE_MS = 10_000;

/**
 * Least-privilege port over the KeyCustodian `getKeyring` op — the TS twin of the
 * .NET `IKeyringClient`. Maps the emitted `GetKeyringOutput` to a validated
 * `@d2/encryption` symmetric keyring (the `aadContext` is used verbatim as the
 * additional-authenticated-data binding, never re-derived client-side).
 */
export interface KeyringClient {
  /**
   * Fetches the symmetric keyring for a key domain.
   *
   * @param keyDomain The key domain to fetch.
   * @param signal Optional cancellation signal.
   */
  getKeyring(
    keyDomain: string,
    signal?: AbortSignal,
  ): Promise<D2Result<PayloadCryptoKeyring>>;
}

/**
 * The gRPC-backed {@link KeyringClient} over the emitted
 * {@link KeyCustodianGrpcClient} facade (dialed over the mTLS channel by
 * the host).
 */
export class GrpcKeyringClient implements KeyringClient {
  readonly #client: KeyCustodianGrpcClient;

  /** @param client The emitted gRPC client facade. */
  constructor(client: KeyCustodianGrpcClient) {
    this.#client = client;
  }

  /** @inheritdoc */
  async getKeyring(
    keyDomain: string,
    signal?: AbortSignal,
  ): Promise<D2Result<PayloadCryptoKeyring>> {
    const result = await this.#client.getKeyring(
      { keyDomain },
      { deadlineMs: _KEYRING_CALL_DEADLINE_MS, signal },
    );

    if (result.failed) {
      return bubbleFail(result);
    }

    if (result.data === undefined) {
      return serviceUnavailable();
    }

    const keyring = new PayloadCryptoKeyring(
      result.data.activeKid,
      new Map(
        result.data.entries.map((e: KeyringEntry) => [e.kid, e.keyBytes]),
      ),
      result.data.aadContext,
    );

    return ok(keyring);
  }
}
