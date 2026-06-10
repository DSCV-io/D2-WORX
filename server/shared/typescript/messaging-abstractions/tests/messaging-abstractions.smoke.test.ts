// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_DLQ_FAILURE_CAUSES,
  ALL_DLQ_FAILURE_METADATA_FIELDS,
  DlqFailureCauses,
  DlqFailureMetadataFields,
} from "../src/index.js";

// The six JSON property-name constants declared in the spec.
// Source: contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json
// Mirrors .NET D2.Shared.Messaging.DlqFailureMetadataFields wire values.
const EXPECTED_METADATA_FIELDS = [
  "cause",
  "errorCode",
  "detail",
  "attemptCount",
  "traceId",
  "nackedBy",
] as const;

// The five closed-enum cause strings declared in the spec.
// Mirrors .NET D2.Shared.Messaging.RabbitMq.Subscribing.DlqFailureCauses wire values.
const EXPECTED_CAUSES = [
  "HANDLER_RESULT_FAILURE",
  "HANDLER_EXCEPTION",
  "DECRYPT_FAILURE",
  "DESERIALIZE_FAILURE",
  "RETRIES_EXHAUSTED",
] as const;

describe("@d2/messaging-abstractions — DlqFailureMetadataFields", () => {
  // long test description — cannot wrap
  it("ALL_DLQ_FAILURE_METADATA_FIELDS contains exactly the six spec-declared JSON property names", () => {
    expect([...ALL_DLQ_FAILURE_METADATA_FIELDS].sort()).toEqual(
      [...EXPECTED_METADATA_FIELDS].sort(),
    );
  });

  it("ALL_DLQ_FAILURE_METADATA_FIELDS has no duplicates", () => {
    expect(new Set(ALL_DLQ_FAILURE_METADATA_FIELDS).size).toBe(
      ALL_DLQ_FAILURE_METADATA_FIELDS.length,
    );
  });

  it("DlqFailureMetadataFields constants match ALL_DLQ_FAILURE_METADATA_FIELDS", () => {
    const catalogSet = new Set<string>(ALL_DLQ_FAILURE_METADATA_FIELDS);
    for (const value of Object.values(DlqFailureMetadataFields))
      expect(catalogSet.has(value)).toBe(true);
    expect(Object.values(DlqFailureMetadataFields)).toHaveLength(
      ALL_DLQ_FAILURE_METADATA_FIELDS.length,
    );
  });

  it("well-known wire values pin the spec-declared JSON property names", () => {
    expect(DlqFailureMetadataFields.CAUSE).toBe("cause");
    expect(DlqFailureMetadataFields.ERROR_CODE).toBe("errorCode");
    expect(DlqFailureMetadataFields.DETAIL).toBe("detail");
    expect(DlqFailureMetadataFields.ATTEMPT_COUNT).toBe("attemptCount");
    expect(DlqFailureMetadataFields.TRACE_ID).toBe("traceId");
    expect(DlqFailureMetadataFields.NACKED_BY).toBe("nackedBy");
  });
});

describe("@d2/messaging-abstractions — DlqFailureCauses", () => {
  it("ALL_DLQ_FAILURE_CAUSES contains exactly the five spec-declared cause strings", () => {
    expect([...ALL_DLQ_FAILURE_CAUSES].sort()).toEqual(
      [...EXPECTED_CAUSES].sort(),
    );
  });

  it("ALL_DLQ_FAILURE_CAUSES has no duplicates", () => {
    expect(new Set(ALL_DLQ_FAILURE_CAUSES).size).toBe(
      ALL_DLQ_FAILURE_CAUSES.length,
    );
  });

  it("DlqFailureCauses constants match ALL_DLQ_FAILURE_CAUSES", () => {
    const catalogSet = new Set<string>(ALL_DLQ_FAILURE_CAUSES);
    for (const value of Object.values(DlqFailureCauses))
      expect(catalogSet.has(value)).toBe(true);
    expect(Object.values(DlqFailureCauses)).toHaveLength(
      ALL_DLQ_FAILURE_CAUSES.length,
    );
  });

  it("well-known cause constants pin the spec-declared wire strings", () => {
    expect(DlqFailureCauses.HANDLER_RESULT_FAILURE).toBe(
      "HANDLER_RESULT_FAILURE",
    );
    expect(DlqFailureCauses.HANDLER_EXCEPTION).toBe("HANDLER_EXCEPTION");
    expect(DlqFailureCauses.DECRYPT_FAILURE).toBe("DECRYPT_FAILURE");
    expect(DlqFailureCauses.DESERIALIZE_FAILURE).toBe("DESERIALIZE_FAILURE");
    expect(DlqFailureCauses.RETRIES_EXHAUSTED).toBe("RETRIES_EXHAUSTED");
  });
});
