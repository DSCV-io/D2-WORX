// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  emitErrorCodes,
  type ErrorCodesSpec,
  validateErrorCodesSpec,
} from "../src/error-codes-emit.js";

const validSpec: ErrorCodesSpec = {
  errorCodes: [
    {
      code: "NOT_FOUND",
      httpStatus: 404,
      doc: "Indicates that the requested resource was not found.",
    },
    {
      code: "SERVICE_UNAVAILABLE",
      httpStatus: 503,
      doc: "Indicates that the service is currently unavailable.",
    },
  ],
};

describe("validateErrorCodesSpec", () => {
  it("happy path returns all entries with no diagnostics", () => {
    const v = validateErrorCodesSpec(validSpec);
    expect(v.entries).toHaveLength(2);
    expect(v.diagnostics).toEqual([]);
  });

  it("flags duplicate codes", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [validSpec.errorCodes[0]!, validSpec.errorCodes[0]!],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC002");
  });

  it("flags unsupported httpStatus", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 418 }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC003");
  });

  it("flags invalid lowercase code", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, code: "lowercase" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags empty code", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, code: "" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags code starting with a digit", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, code: "9NOPE" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC004");
  });

  it("flags missing/empty doc", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, doc: "" }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC005");
  });

  it("flags whitespace-only doc", () => {
    const v = validateErrorCodesSpec({
      errorCodes: [{ ...validSpec.errorCodes[0]!, doc: "   " }],
    });
    expect(v.diagnostics[0]?.id).toBe("D2EC005");
  });
});

describe("emitErrorCodes — snapshot pin", () => {
  it("emits constants + http-status switch in spec order", () => {
    const r = emitErrorCodes(validSpec);
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain('NOT_FOUND: "NOT_FOUND"');
    expect(r.source).toContain('SERVICE_UNAVAILABLE: "SERVICE_UNAVAILABLE"');
    expect(r.source).toContain(
      "export const ALL_ERROR_CODES: readonly string[]",
    );
    expect(r.source).toContain('case "NOT_FOUND": return 404;');
    expect(r.source).toContain('case "SERVICE_UNAVAILABLE": return 503;');
    // Spec order: NOT_FOUND first, SERVICE_UNAVAILABLE second.
    expect(
      r.source.indexOf("NOT_FOUND") < r.source.indexOf("SERVICE_UNAVAILABLE"),
    ).toBe(true);
  });

  it("blocks emit on validation diagnostics", () => {
    const r = emitErrorCodes({
      errorCodes: [{ ...validSpec.errorCodes[0]!, httpStatus: 999 }],
    });
    expect(r.source).toBe("");
    expect(r.diagnostics).not.toEqual([]);
  });

  it("produces identical source across two runs (idempotency)", () => {
    const first = emitErrorCodes(validSpec).source;
    const second = emitErrorCodes(validSpec).source;
    expect(second).toBe(first);
  });

  it("escapes JSDoc-terminator sequences in doc text", () => {
    const r = emitErrorCodes({
      errorCodes: [
        {
          code: "TRICKY",
          httpStatus: 400,
          doc: "Has */ inside.",
        },
      ],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain("Has *\\/ inside.");
  });
});

describe("emitErrorCodes — per-VALUE pin for the shipping spec", () => {
  it.each([
    ["NOT_FOUND", 404],
    ["FORBIDDEN", 403],
    ["UNAUTHORIZED", 401],
    ["VALIDATION_FAILED", 400],
    ["CONFLICT", 409],
    ["UNHANDLED_EXCEPTION", 500],
    ["COULD_NOT_BE_SERIALIZED", 500],
    ["COULD_NOT_BE_DESERIALIZED", 500],
    ["SERVICE_UNAVAILABLE", 503],
    ["SOME_FOUND", 206],
    ["PARTIAL_SUCCESS", 207],
    ["RATE_LIMITED", 429],
    ["IDEMPOTENCY_IN_FLIGHT", 409],
    ["PAYLOAD_TOO_LARGE", 413],
    ["CANCELED", 400],
  ])("entry %s maps to httpStatus %s", (code, httpStatus) => {
    const r = emitErrorCodes({
      errorCodes: [{ code, httpStatus, doc: `${code} doc.` }],
    });
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toContain(`${code}: "${code}"`);
    expect(r.source).toContain(`case "${code}": return ${httpStatus};`);
  });
});
