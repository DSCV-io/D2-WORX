// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import {
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "@d2/messaging-abstractions";
import { fail, tk } from "@d2/result";
import { describe, expect, it } from "vitest";

import { DlqFailureHeaderBuilder } from "../src/subscribing/dlq-failure-metadata.js";
import { MessageBodyDecodeError } from "../src/subscribing/message-body-decode-error.js";

function decode(header: string): Record<string, unknown> {
  return JSON.parse(header) as Record<string, unknown>;
}

describe("DlqFailureHeaderBuilder — .NET DlqFailureHeaderBuilder parity", () => {
  it("fromException: cause + error type name, no detail, PII-safe", () => {
    const meta = decode(
      DlqFailureHeaderBuilder.fromException(new TypeError("secret user input")),
    );
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_EXCEPTION,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("TypeError");
    // detail is NEVER built from the exception message (which held user input).
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBeUndefined();
    expect(meta[DlqFailureMetadataFields.ATTEMPT_COUNT]).toBe(0);
    expect(JSON.stringify(meta)).not.toContain("secret user input");
  });

  it("fromResult: joins message KEYS (translation tokens) into detail", () => {
    const result = fail({
      errorCode: "KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE",
      messages: [
        tk("keycustodian.errors.SEAL_KEY_UNAVAILABLE"),
        tk("common.errors.RETRY"),
      ],
    });
    const meta = decode(DlqFailureHeaderBuilder.fromResult(result));
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.HANDLER_RESULT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "KEYCUSTODIAN_SEAL_KEY_UNAVAILABLE",
    );
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBe(
      "keycustodian.errors.SEAL_KEY_UNAVAILABLE; common.errors.RETRY",
    );
  });

  it("fromResult: unknown error code + empty messages omit detail", () => {
    const meta = decode(DlqFailureHeaderBuilder.fromResult(fail()));
    // Pins the code-absent fallback to the wire literal that the .NET sibling
    // (`DlqFailureHeaderBuilder.cs:76`) also emits — both runtimes stay in
    // byte parity if the local `_UNKNOWN_ERROR_CODE` sentinel ever drifts.
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe("UNKNOWN");
    expect(meta[DlqFailureMetadataFields.DETAIL]).toBeUndefined();
  });

  it("fromResult: truncates an overlong detail to 256 chars", () => {
    const result = fail({ errorCode: "X", messages: [tk("a".repeat(400))] });
    const meta = decode(DlqFailureHeaderBuilder.fromResult(result));
    expect((meta[DlqFailureMetadataFields.DETAIL] as string).length).toBe(256);
  });

  it("fromRetriesExhausted: cause + errorCode both RETRIES_EXHAUSTED with count", () => {
    const meta = decode(DlqFailureHeaderBuilder.fromRetriesExhausted(5));
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.RETRIES_EXHAUSTED,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      DlqFailureCauses.RETRIES_EXHAUSTED,
    );
    expect(meta[DlqFailureMetadataFields.ATTEMPT_COUNT]).toBe(5);
  });

  it("fromBoundary: carries the given cause + inner error name", () => {
    const meta = decode(
      DlqFailureHeaderBuilder.fromBoundary(
        DlqFailureCauses.DECRYPT_FAILURE,
        new MessageBodyDecodeError(
          DlqFailureCauses.DECRYPT_FAILURE,
          "no opener",
        ),
      ),
    );
    expect(meta[DlqFailureMetadataFields.CAUSE]).toBe(
      DlqFailureCauses.DECRYPT_FAILURE,
    );
    expect(meta[DlqFailureMetadataFields.ERROR_CODE]).toBe(
      "MessageBodyDecodeError",
    );
  });

  it("includes traceId + nackedBy when supplied; omits them when absent", () => {
    const withCtx = decode(
      DlqFailureHeaderBuilder.fromException(new Error("x"), {
        traceId: "abc123",
        nackedBy: "audit-svc",
      }),
    );
    expect(withCtx[DlqFailureMetadataFields.TRACE_ID]).toBe("abc123");
    expect(withCtx[DlqFailureMetadataFields.NACKED_BY]).toBe("audit-svc");

    const withoutCtx = decode(
      DlqFailureHeaderBuilder.fromException(new Error("x")),
    );
    expect(DlqFailureMetadataFields.TRACE_ID in withoutCtx).toBe(false);
    expect(DlqFailureMetadataFields.NACKED_BY in withoutCtx).toBe(false);
  });
});
