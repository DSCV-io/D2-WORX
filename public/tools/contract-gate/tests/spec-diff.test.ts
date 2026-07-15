// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import {
  diffFlatCatalog,
  diffNestedCatalog,
  diffCatalog,
} from "../src/spec-diff.js";
import type {
  FlatCatalogIdentity,
  MultiCatalogIdentity,
  NestedCatalogIdentity,
} from "../src/catalog-identity.js";

// ---------------------------------------------------------------------------
// Fixture helpers
// ---------------------------------------------------------------------------

const FIXTURES_DIR = join(
  fileURLToPath(import.meta.url),
  "..",
  "fixtures",
  "spec",
);

function readFixture(subPath: string): unknown {
  const content = readFileSync(join(FIXTURES_DIR, subPath), "utf-8");
  return JSON.parse(content) as unknown;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const ERROR_CODE_IDENTITY: FlatCatalogIdentity = {
  kind: "flat",
  arrayProp: "errorCodes",
  idField: "code",
};

function makeErrorCodes(...codes: string[]) {
  return {
    errorCodes: codes.map((code) => ({
      code,
      httpStatus: 400,
      category: "validation_failure",
      doc: `Doc for ${code}`,
    })),
  };
}

// ---------------------------------------------------------------------------
// diffFlatCatalog — removed entry
// ---------------------------------------------------------------------------

describe("diffFlatCatalog — removed entry", () => {
  it("returns a finding when an entry is removed", () => {
    const before = makeErrorCodes("CODE_A", "CODE_B", "CODE_C");
    const after = makeErrorCodes("CODE_A", "CODE_B");

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.arm).toBe("spec");
    expect(findings[0]?.severity).toBe("ERROR");
    expect(findings[0]?.message).toContain("CODE_C");
    expect(findings[0]?.message).toContain("removed");
  });

  it("matches the worked-example gate failure text shape", () => {
    const before = makeErrorCodes("NOT_FOUND");
    const after = { errorCodes: [] };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "contracts/error-codes/error-codes.spec.json",
    );

    expect(findings).toHaveLength(1);
    // The finding message must carry: BREAKING, the removed entry ID, "removed",
    // "Gate FAILED", and a hint about the force valve.
    expect(findings[0]?.message).toContain("BREAKING");
    expect(findings[0]?.message).toContain("NOT_FOUND");
    expect(findings[0]?.message).toContain("removed");
    expect(findings[0]?.message).toContain("Gate FAILED");
    expect(findings[0]?.message).toContain("force valve");
  });

  it("returns a finding for a deprecated entry that is deleted (deprecated-or-not, deletion fails)", () => {
    const before = {
      errorCodes: [
        { code: "OLD_CODE", httpStatus: 400, doc: "x", deprecated: true },
      ],
    };
    const after = { errorCodes: [] };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("OLD_CODE");
    // The message should mention deprecated entries still fail
    expect(findings[0]?.message).toContain("deprecated");
  });

  it("passes when the deleted entry had never been in baseline (fully additive — no findings)", () => {
    const before = { errorCodes: [] }; // baseline had empty catalog
    const after = makeErrorCodes("NEW_CODE");

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffFlatCatalog — retyped value
// ---------------------------------------------------------------------------

describe("diffFlatCatalog — retyped field value", () => {
  it("returns a finding when httpStatus changes from number to string", () => {
    const before = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400, doc: "x" }],
    };
    const after = {
      errorCodes: [{ code: "CODE_A", httpStatus: "400", doc: "x" }],
    };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("CODE_A");
    expect(findings[0]?.message).toContain("httpStatus");
  });

  it("returns a finding when httpStatus value changes (400 → 500)", () => {
    const before = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400, doc: "x" }],
    };
    const after = {
      errorCodes: [{ code: "CODE_A", httpStatus: 500, doc: "x" }],
    };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("httpStatus");
    expect(findings[0]?.message).toContain("400");
    expect(findings[0]?.message).toContain("500");
  });

  it("returns a finding when a field is removed from a surviving entry", () => {
    const before = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400, doc: "x" }],
    };
    const after = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400 }],
    };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("doc");
  });
});

// ---------------------------------------------------------------------------
// diffFlatCatalog — additive changes (PASS)
// ---------------------------------------------------------------------------

describe("diffFlatCatalog — additive changes are PASS", () => {
  it("adding a new entry to the catalog is not a break", () => {
    const before = makeErrorCodes("CODE_A");
    const after = makeErrorCodes("CODE_A", "CODE_B");

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("adding a new optional field to a surviving entry is not a break", () => {
    const before = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400, doc: "x" }],
    };
    const after = {
      errorCodes: [
        {
          code: "CODE_A",
          httpStatus: 400,
          doc: "x",
          newOptionalField: "value",
        },
      ],
    };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("marking an entry deprecated (deprecated: false → true) is not a break", () => {
    const before = {
      errorCodes: [
        { code: "CODE_A", httpStatus: 400, doc: "x", deprecated: false },
      ],
    };
    const after = {
      errorCodes: [
        { code: "CODE_A", httpStatus: 400, doc: "x", deprecated: true },
      ],
    };

    // deprecated field changes are ignored by the differ
    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("adding deprecated: true to an entry that had no deprecated field is not a break", () => {
    const before = {
      errorCodes: [{ code: "CODE_A", httpStatus: 400, doc: "x" }],
    };
    const after = {
      errorCodes: [
        { code: "CODE_A", httpStatus: 400, doc: "x", deprecated: true },
      ],
    };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("reordering entries (different array index, same ids) is not a break", () => {
    const before = makeErrorCodes("CODE_A", "CODE_B", "CODE_C");
    const after = makeErrorCodes("CODE_C", "CODE_A", "CODE_B");

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("baseline missing the arrayProp (new catalog) produces no findings (fully additive)", () => {
    const before = {}; // baseline has no errorCodes array
    const after = makeErrorCodes("CODE_A");

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffFlatCatalog — adversarial / malformed inputs
// ---------------------------------------------------------------------------

describe("diffFlatCatalog — adversarial inputs", () => {
  it("throws when before is not an object (malformed JSON baseline)", () => {
    expect(() =>
      diffFlatCatalog(
        "not-an-object",
        {},
        ERROR_CODE_IDENTITY,
        "test.spec.json",
      ),
    ).toThrow();
  });

  it("throws when after is not an object (malformed JSON proposed)", () => {
    expect(() =>
      diffFlatCatalog(
        {},
        "not-an-object",
        ERROR_CODE_IDENTITY,
        "test.spec.json",
      ),
    ).toThrow();
  });

  it("throws when after[arrayProp] is not an array (malformed proposed catalog)", () => {
    const before = makeErrorCodes("CODE_A");
    const after = { errorCodes: "not-an-array" };

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow();
  });

  it("throws when a catalog entry is not an object", () => {
    const before = { errorCodes: ["not-an-object"] };
    const after = makeErrorCodes("CODE_A");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow();
  });

  it("throws when a catalog entry is missing the identity field", () => {
    const before = {
      errorCodes: [{ httpStatus: 400 }], // no 'code' field
    };
    const after = makeErrorCodes("CODE_A");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow();
  });

  it("throws when a catalog identity is an empty string", () => {
    const before = {
      errorCodes: [{ code: "", httpStatus: 400 }],
    };
    const after = makeErrorCodes("CODE_A");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow(/missing string identity field/);
  });

  it("throws when a catalog identity is whitespace-only", () => {
    const before = {
      errorCodes: [{ code: "   ", httpStatus: 400 }],
    };
    const after = makeErrorCodes("CODE_A");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow(/missing string identity field/);
  });

  it("throws on duplicate identity in the before catalog", () => {
    const before = {
      errorCodes: [
        { code: "DUPE", httpStatus: 400 },
        { code: "DUPE", httpStatus: 500 },
      ],
    };
    const after = makeErrorCodes("DUPE");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow(/duplicate/i);
  });

  it("handles null entries in the array by throwing (fail-loud)", () => {
    const before = { errorCodes: [null] };
    const after = makeErrorCodes("CODE_A");

    expect(() =>
      diffFlatCatalog(before, after, ERROR_CODE_IDENTITY, "test.spec.json"),
    ).toThrow();
  });

  it("handles empty catalog arrays (no entries → no findings)", () => {
    const before = { errorCodes: [] };
    const after = { errorCodes: [] };

    const findings = diffFlatCatalog(
      before,
      after,
      ERROR_CODE_IDENTITY,
      "test.spec.json",
    );
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffCatalog — exempt catalog (geo $generated)
// ---------------------------------------------------------------------------

describe("diffCatalog — exempt catalog", () => {
  it("returns no findings for an exempt catalog even with removed entries", () => {
    const before = {
      entries: [
        { code: "en", name: "English" },
        { code: "fr", name: "French" },
      ],
    };
    const after = { entries: [{ code: "en", name: "English" }] };

    const findings = diffCatalog(
      before,
      after,
      { kind: "exempt", reason: "geo Tier-2 generated" },
      "contracts/geo/languages.spec.json",
    );

    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffCatalog — undefined/null baseline (fully additive)
// ---------------------------------------------------------------------------

describe("diffCatalog — null/undefined baseline", () => {
  it("returns no findings when baseline is undefined (new file)", () => {
    const findings = diffCatalog(
      undefined,
      makeErrorCodes("CODE_A"),
      ERROR_CODE_IDENTITY,
      "new.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("returns no findings when baseline is null", () => {
    const findings = diffCatalog(
      null,
      makeErrorCodes("CODE_A"),
      ERROR_CODE_IDENTITY,
      "new.spec.json",
    );
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffNestedCatalog — telemetry (meters → instruments → tags → values)
// ---------------------------------------------------------------------------

describe("diffNestedCatalog — telemetry catalog", () => {
  const TELEMETRY_IDENTITY: NestedCatalogIdentity = {
    kind: "nested",
    arrayProp: "meters",
    idField: "meter",
    nested: {
      kind: "flat",
      arrayProp: "instruments",
      idField: "name",
      nested: {
        kind: "flat",
        arrayProp: "tags",
        idField: "name",
        valuesArrayProp: "values",
      },
    },
  };

  function makeDoc(
    meterName: string,
    instrumentName: string,
    tagName: string,
    tagValues: string[],
  ) {
    return {
      meters: [
        {
          meter: meterName,
          instruments: [
            {
              name: instrumentName,
              tags: [{ name: tagName, values: tagValues }],
            },
          ],
        },
      ],
    };
  }

  it("returns no findings when telemetry catalog is unchanged", () => {
    const doc = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success", "failure"],
    );

    const findings = diffNestedCatalog(
      doc,
      doc,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("returns a finding when a meter is removed", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success"],
    );
    const after = { meters: [] };

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("D2.Shared.Auth");
  });

  it("returns a finding when an instrument is removed from a meter", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success"],
    );
    const after = {
      meters: [{ meter: "D2.Shared.Auth", instruments: [] }],
    };

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("d2.auth.jwt.validations");
  });

  it("returns a finding when a tag is removed from an instrument", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success"],
    );
    const after = {
      meters: [
        {
          meter: "D2.Shared.Auth",
          instruments: [{ name: "d2.auth.jwt.validations", tags: [] }],
        },
      ],
    };

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("outcome");
  });

  it("returns a finding when a tag VALUE is removed", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success", "failure", "expired"],
    );
    const after = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success", "failure"],
    );

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("expired");
  });

  it("adding a new tag value is not a break", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success"],
    );
    const after = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success", "new_value"],
    );

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(0);
  });

  it("adding a new instrument to an existing meter is not a break", () => {
    const before = makeDoc(
      "D2.Shared.Auth",
      "d2.auth.jwt.validations",
      "outcome",
      ["success"],
    );
    const after = {
      meters: [
        {
          meter: "D2.Shared.Auth",
          instruments: [
            {
              name: "d2.auth.jwt.validations",
              tags: [{ name: "outcome", values: ["success"] }],
            },
            { name: "d2.auth.new.counter", tags: [] },
          ],
        },
      ],
    };

    const findings = diffNestedCatalog(
      before,
      after,
      TELEMETRY_IDENTITY,
      "telemetry.spec.json",
    );
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// diffCatalog — multi-catalog identity (field-constraints: constraints + enums)
// ---------------------------------------------------------------------------

describe("diffCatalog — multi-catalog identity: enum member deletion is a gate break", () => {
  // Regression: before this fix, removing an enum member from field-constraints.spec.json
  // produced ZERO findings because the gate only registered constraints[]; enums[] was
  // invisible. This test proves the blind spot is closed.

  const FIELD_CONSTRAINTS_IDENTITY: MultiCatalogIdentity = {
    kind: "multi",
    parts: [
      { kind: "flat", arrayProp: "constraints", idField: "name" },
      {
        kind: "nested",
        arrayProp: "enums",
        idField: "name",
        nested: { kind: "flat", arrayProp: "members", idField: "name" },
      },
    ],
  };

  it("removing an enum member produces a finding (breaking — gate blind spot regression)", () => {
    const before = readFixture("before/field-constraints.spec.json");
    const after = readFixture(
      "after/field-constraints-enum-member-removed.spec.json",
    );

    const findings = diffCatalog(
      before,
      after,
      FIELD_CONSTRAINTS_IDENTITY,
      "contracts/validation/field-constraints.spec.json",
    );

    expect(findings.length).toBeGreaterThan(0);
    expect(findings[0]?.arm).toBe("spec");
    expect(findings[0]?.severity).toBe("ERROR");
    expect(findings[0]?.message).toContain("Intersex");
    expect(findings[0]?.message).toContain("removed");
  });

  it("adding an enum member is not a break (additive)", () => {
    const before = readFixture("before/field-constraints.spec.json");
    const after = readFixture(
      "after/field-constraints-enum-member-added.spec.json",
    );

    const findings = diffCatalog(
      before,
      after,
      FIELD_CONSTRAINTS_IDENTITY,
      "contracts/validation/field-constraints.spec.json",
    );

    expect(findings).toHaveLength(0);
  });

  it("removing an enum itself (not just a member) produces a finding", () => {
    const before = readFixture("before/field-constraints.spec.json");
    const after = {
      constraints: [
        {
          name: "FIRST_NAME_MAX",
          value: 255,
          doc: "Maximum length of a first name.",
        },
      ],
      enums: [],
    };

    const findings = diffCatalog(
      before,
      after,
      FIELD_CONSTRAINTS_IDENTITY,
      "contracts/validation/field-constraints.spec.json",
    );

    expect(findings.length).toBeGreaterThan(0);
    expect(findings[0]?.message).toContain("BiologicalSex");
  });

  it("constraints removal still produces a finding when enums are unchanged", () => {
    const before = readFixture("before/field-constraints.spec.json");
    const after = {
      constraints: [],
      enums: (before as { enums: unknown[] }).enums,
    };

    const findings = diffCatalog(
      before,
      after,
      FIELD_CONSTRAINTS_IDENTITY,
      "contracts/validation/field-constraints.spec.json",
    );

    expect(findings.length).toBeGreaterThan(0);
    expect(findings[0]?.message).toContain("FIRST_NAME_MAX");
  });
});

// ---------------------------------------------------------------------------
// diffCatalog — multi-catalog identity: dlq-failure-metadata (fields + causes)
// ---------------------------------------------------------------------------

describe("diffCatalog — multi-catalog identity: dlq-failure-metadata causes deletion is a gate break", () => {
  // Regression: before this fix, removing a cause from dlq-failure-metadata.spec.json
  // produced ZERO findings because the gate only registered fields[]; causes[] was
  // invisible. This test proves the blind spot is closed.

  const DLQ_IDENTITY: MultiCatalogIdentity = {
    kind: "multi",
    parts: [
      { kind: "flat", arrayProp: "fields", idField: "constName" },
      { kind: "flat", arrayProp: "causes", idField: "constName" },
    ],
  };

  it("removing a cause entry produces a finding (breaking — gate blind spot regression)", () => {
    const before = readFixture("before/dlq-failure-metadata.spec.json");
    const after = readFixture(
      "after/dlq-failure-metadata-cause-removed.spec.json",
    );

    const findings = diffCatalog(
      before,
      after,
      DLQ_IDENTITY,
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );

    expect(findings.length).toBeGreaterThan(0);
    expect(findings[0]?.arm).toBe("spec");
    expect(findings[0]?.severity).toBe("ERROR");
    expect(findings[0]?.message).toContain("DECRYPT_FAILURE");
    expect(findings[0]?.message).toContain("removed");
  });

  it("removing a cause entry is detected even when fields[] is unchanged", () => {
    const before = readFixture("before/dlq-failure-metadata.spec.json");
    const after = readFixture(
      "after/dlq-failure-metadata-cause-removed.spec.json",
    );

    const findings = diffCatalog(
      before,
      after,
      DLQ_IDENTITY,
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );

    // Must produce exactly one finding (the removed cause); fields[] is identical.
    expect(findings).toHaveLength(1);
    expect(findings[0]?.message).toContain("DECRYPT_FAILURE");
  });

  it("removing a field entry (not a cause) is still detected (both parts gated)", () => {
    const before = readFixture("before/dlq-failure-metadata.spec.json");
    const afterWithFieldRemoved = {
      fields: [
        {
          constName: "CAUSE",
          value: "cause",
          doc: "JSON property carrying the closed-enum cause string.",
        },
      ],
      causes: (before as { causes: unknown[] }).causes,
    };

    const findings = diffCatalog(
      before,
      afterWithFieldRemoved,
      DLQ_IDENTITY,
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );

    expect(findings.length).toBeGreaterThan(0);
    expect(findings[0]?.message).toContain("ERROR_CODE");
  });

  it("unchanged dlq-failure-metadata produces no findings (PASS)", () => {
    const before = readFixture("before/dlq-failure-metadata.spec.json");

    const findings = diffCatalog(
      before,
      before,
      DLQ_IDENTITY,
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );

    expect(findings).toHaveLength(0);
  });

  it("adding a new cause is not a break (additive)", () => {
    const before = readFixture("before/dlq-failure-metadata.spec.json");
    const afterWithAddedCause = {
      fields: (before as { fields: unknown[] }).fields,
      causes: [
        ...(before as { causes: unknown[] }).causes,
        {
          constName: "RETRIES_EXHAUSTED",
          value: "RETRIES_EXHAUSTED",
          doc: "New cause.",
        },
      ],
    };

    const findings = diffCatalog(
      before,
      afterWithAddedCause,
      DLQ_IDENTITY,
      "contracts/dlq-failure-metadata/dlq-failure-metadata.spec.json",
    );

    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Real-fixture tests — spec file pairs (non-vacuous coverage via actual files)
// ---------------------------------------------------------------------------

describe("diffCatalog — real fixture: error-codes.spec.json before/after (entry removed)", () => {
  it("error-codes-removed fixture: detects the removed CONFLICT entry as a gate break", () => {
    const before = readFixture("before/error-codes.spec.json");
    const after = readFixture("after/error-codes-removed.spec.json");

    const findings = diffCatalog(
      before,
      after,
      { kind: "flat", arrayProp: "errorCodes", idField: "code" },
      "contracts/error-codes/error-codes.spec.json",
    );

    expect(findings).toHaveLength(1);
    expect(findings[0]?.arm).toBe("spec");
    expect(findings[0]?.message).toContain("CONFLICT");
    expect(findings[0]?.message).toContain("removed");
  });

  it("error-codes fixture unchanged: no findings (PASS)", () => {
    const before = readFixture("before/error-codes.spec.json");

    const findings = diffCatalog(
      before,
      before,
      { kind: "flat", arrayProp: "errorCodes", idField: "code" },
      "contracts/error-codes/error-codes.spec.json",
    );

    expect(findings).toHaveLength(0);
  });
});

describe("diffCatalog — real fixture: geo-generated.spec.json before/after (exempt — no findings)", () => {
  it("geo-generated entry-removed fixture: produces no findings (exempt catalog)", () => {
    const before = readFixture("before/geo-generated.spec.json");
    const after = readFixture("after/geo-generated-entry-removed.spec.json");

    const findings = diffCatalog(
      before,
      after,
      {
        kind: "exempt",
        reason: "Geo Tier-2 $generated spec — regenerable pipeline output",
      },
      "contracts/geo/languages.spec.json",
    );

    expect(findings).toHaveLength(0);
  });
});
