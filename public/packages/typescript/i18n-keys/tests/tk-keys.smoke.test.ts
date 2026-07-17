// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { TK } from "../src/index.js";

describe("TK constants", () => {
  it("common.errors constants are TKMessage instances keyed by snake-case keys", () => {
    expect(TK.common.errors.NOT_FOUND.key).toBe("common_errors_NOT_FOUND");
    expect(TK.common.errors.CONFLICT.key).toBe("common_errors_CONFLICT");
    expect(TK.common.errors.UNAUTHORIZED.key).toBe(
      "common_errors_UNAUTHORIZED",
    );
    expect(TK.common.errors.FORBIDDEN.key).toBe("common_errors_FORBIDDEN");
    expect(TK.common.errors.VALIDATION_FAILED.key).toBe(
      "common_errors_VALIDATION_FAILED",
    );
    expect(TK.common.errors.SERVICE_UNAVAILABLE.key).toBe(
      "common_errors_SERVICE_UNAVAILABLE",
    );
    expect(TK.common.errors.UNKNOWN.key).toBe("common_errors_UNKNOWN");
    expect(TK.common.errors.PAYLOAD_TOO_LARGE.key).toBe(
      "common_errors_PAYLOAD_TOO_LARGE",
    );
    expect(TK.common.errors.TOO_MANY_REQUESTS.key).toBe(
      "common_errors_TOO_MANY_REQUESTS",
    );
    expect(TK.common.errors.CANCELED.key).toBe("common_errors_CANCELED");
    expect(TK.common.errors.IDEMPOTENCY_IN_FLIGHT.key).toBe(
      "common_errors_IDEMPOTENCY_IN_FLIGHT",
    );
  });

  it("auth.errors constants are TKMessage instances keyed by snake-case keys", () => {
    expect(TK.auth.errors.UNAUTHORIZED.key).toBe("auth_errors_UNAUTHORIZED");
    expect(TK.auth.errors.TEMPORARILY_UNAVAILABLE.key).toBe(
      "auth_errors_TEMPORARILY_UNAVAILABLE",
    );
  });

  it("every leaf is a TKMessage whose key is a non-empty string with no params", () => {
    const checkNode = (node: unknown): void => {
      if (
        typeof node === "object" &&
        node !== null &&
        "key" in node &&
        typeof (node as { key: unknown }).key === "string"
      ) {
        // TKMessage leaf — its key must be a non-empty string and it must
        // carry no params (the catalog constants are bare key messages).
        const message = node as { key: string; params?: unknown };
        expect(message.key.length).toBeGreaterThan(0);
        expect(message.params).toBeUndefined();
      } else if (typeof node === "object" && node !== null) {
        for (const value of Object.values(node)) {
          checkNode(value);
        }
      } else {
        // The catalog has no non-object, non-TKMessage leaves.
        throw new Error(`unexpected non-TKMessage leaf: ${String(node)}`);
      }
    };
    checkNode(TK);
  });
});
