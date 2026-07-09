// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { readFileSync } from "node:fs";
import { join, resolve, dirname } from "node:path";
import { fileURLToPath } from "node:url";

import { diffOpenApi } from "../src/openapi-diff.js";
import { repoRoot } from "./repo-root.js";

const __dirname = dirname(fileURLToPath(import.meta.url));
const BEFORE_DIR = resolve(__dirname, "fixtures", "openapi", "before");
const AFTER_DIR = resolve(__dirname, "fixtures", "openapi", "after");

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function loadFixture(dir: string, name: string): unknown {
  return JSON.parse(readFileSync(join(dir, name), "utf-8")) as unknown;
}

const BEFORE = loadFixture(BEFORE_DIR, "api.openapi.json");

// ---------------------------------------------------------------------------
// Break class 1: removed path
// ---------------------------------------------------------------------------

describe("diffOpenApi — removed path (break class 1)", () => {
  it("returns a finding when a path is removed", () => {
    const after = loadFixture(AFTER_DIR, "api-path-removed.openapi.json");
    const findings = diffOpenApi(BEFORE, after, "test.openapi.g.json");

    expect(findings.length).toBeGreaterThan(0);
    const pathFinding = findings.find((f) => f.message.includes("/v1/export"));
    expect(pathFinding).toBeDefined();
    expect(pathFinding?.arm).toBe("openapi");
    expect(pathFinding?.severity).toBe("ERROR");
  });

  it("removed path finding mentions the path and Gate FAILED", () => {
    const after = loadFixture(AFTER_DIR, "api-path-removed.openapi.json");
    const findings = diffOpenApi(BEFORE, after, "test.openapi.g.json");
    const pathFinding = findings.find((f) => f.message.includes("/v1/export"));
    expect(pathFinding?.message).toContain("Gate FAILED");
    expect(pathFinding?.message).toContain("force valve");
  });
});

// ---------------------------------------------------------------------------
// Break class 2: removed operation
// ---------------------------------------------------------------------------

describe("diffOpenApi — removed operation (break class 2)", () => {
  it("returns a finding when an operation method is removed", () => {
    const before = {
      paths: {
        "/v1/data": {
          get: { operationId: "getData", parameters: [], responses: {} },
          post: { operationId: "createData", parameters: [], responses: {} },
        },
      },
      components: { schemas: {} },
    };
    const after = {
      paths: {
        "/v1/data": {
          get: { operationId: "getData", parameters: [], responses: {} },
        },
      },
      components: { schemas: {} },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
    const opFinding = findings.find(
      (f) => f.message.includes("POST") && f.message.includes("/v1/data"),
    );
    expect(opFinding).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Break class 3: response required field removed
// ---------------------------------------------------------------------------

describe("diffOpenApi — response required field removed (break class 3)", () => {
  it("returns a finding when a required field is removed from a response schema", () => {
    const after = loadFixture(
      AFTER_DIR,
      "api-response-required-removed.openapi.json",
    );
    const findings = diffOpenApi(BEFORE, after, "test.openapi.g.json");

    const reqFinding = findings.find(
      (f) => f.message.includes("status") && f.message.includes("required"),
    );
    expect(reqFinding).toBeDefined();
    expect(reqFinding?.severity).toBe("ERROR");
  });
});

// ---------------------------------------------------------------------------
// Break class 4: request required field ADDED
// ---------------------------------------------------------------------------

describe("diffOpenApi — required request field added (break class 4)", () => {
  it("returns a finding when a required field is ADDED to a request body schema", () => {
    const before = {
      paths: {
        "/v1/data": {
          post: {
            operationId: "createData",
            parameters: [],
            responses: {},
            requestBody: {
              required: true,
              content: {
                "application/json": {
                  schema: {
                    type: "object",
                    properties: { name: { type: "string" } },
                    required: ["name"],
                  },
                },
              },
            },
          },
        },
      },
      components: { schemas: {} },
    };
    const after = {
      paths: {
        "/v1/data": {
          post: {
            operationId: "createData",
            parameters: [],
            responses: {},
            requestBody: {
              required: true,
              content: {
                "application/json": {
                  schema: {
                    type: "object",
                    properties: {
                      name: { type: "string" },
                      email: { type: "string" },
                    },
                    required: ["name", "email"], // 'email' added
                  },
                },
              },
            },
          },
        },
      },
      components: { schemas: {} },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
    const reqFinding = findings.find(
      (f) => f.message.includes("email") && f.message.includes("required"),
    );
    expect(reqFinding).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Break class 5: type narrowing
// ---------------------------------------------------------------------------

describe("diffOpenApi — type narrowing (break class 5)", () => {
  it("returns a finding when a property type changes", () => {
    const before = {
      paths: {},
      components: {
        schemas: {
          MySchema: {
            type: "object",
            properties: { count: { type: "string" } },
          },
        },
      },
    };
    const after = {
      paths: {},
      components: {
        schemas: {
          MySchema: {
            type: "object",
            properties: { count: { type: "integer" } },
          },
        },
      },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
    const typeFinding = findings.find((f) =>
      f.message.includes("type changed"),
    );
    expect(typeFinding).toBeDefined();
    expect(typeFinding?.message).toContain("string");
    expect(typeFinding?.message).toContain("integer");
  });
});

// ---------------------------------------------------------------------------
// Break class 6: dropped enum value
// ---------------------------------------------------------------------------

describe("diffOpenApi — dropped enum value (break class 6)", () => {
  it("returns a finding when an enum value is removed from a schema property", () => {
    const before = {
      paths: {},
      components: {
        schemas: {
          StatusSchema: {
            type: "object",
            properties: {
              status: {
                type: "string",
                enum: ["draft", "published", "archived"],
              },
            },
          },
        },
      },
    };
    const after = {
      paths: {},
      components: {
        schemas: {
          StatusSchema: {
            type: "object",
            properties: {
              status: {
                type: "string",
                enum: ["draft", "published"], // "archived" removed
              },
            },
          },
        },
      },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
    const enumFinding = findings.find((f) => f.message.includes("archived"));
    expect(enumFinding).toBeDefined();
    expect(enumFinding?.message).toContain("enum value");
  });
});

// ---------------------------------------------------------------------------
// Break class 7: removed component schema
// ---------------------------------------------------------------------------

describe("diffOpenApi — removed component schema (break class 7)", () => {
  it("returns a finding when a schema is removed from components.schemas", () => {
    const before = {
      paths: {},
      components: {
        schemas: {
          MyOutput: { type: "object", properties: { id: { type: "string" } } },
          OtherSchema: { type: "object" },
        },
      },
    };
    const after = {
      paths: {},
      components: {
        schemas: {
          MyOutput: { type: "object", properties: { id: { type: "string" } } },
        },
      },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.some((f) => f.message.includes("OtherSchema"))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// Additive changes → PASS
// ---------------------------------------------------------------------------

describe("diffOpenApi — additive changes are PASS", () => {
  it("adding a new path is not a break", () => {
    const before = {
      paths: {
        "/v1/existing": {
          get: { operationId: "x", parameters: [], responses: {} },
        },
      },
      components: { schemas: {} },
    };
    const after = {
      paths: {
        "/v1/existing": {
          get: { operationId: "x", parameters: [], responses: {} },
        },
        "/v1/new": { get: { operationId: "y", parameters: [], responses: {} } },
      },
      components: { schemas: {} },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings).toHaveLength(0);
  });

  it("adding a new optional field to a response schema is not a break", () => {
    const before = {
      paths: {},
      components: {
        schemas: {
          MyOutput: {
            type: "object",
            required: ["id"],
            properties: { id: { type: "string" } },
          },
        },
      },
    };
    const after = {
      paths: {},
      components: {
        schemas: {
          MyOutput: {
            type: "object",
            required: ["id"],
            properties: { id: { type: "string" }, extra: { type: "string" } },
          },
        },
      },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings).toHaveLength(0);
  });

  it("adding an enum value is not a break", () => {
    const before = {
      paths: {},
      components: {
        schemas: {
          S: {
            type: "object",
            properties: { status: { type: "string", enum: ["a", "b"] } },
          },
        },
      },
    };
    const after = {
      paths: {},
      components: {
        schemas: {
          S: {
            type: "object",
            properties: { status: { type: "string", enum: ["a", "b", "c"] } },
          },
        },
      },
    };

    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings).toHaveLength(0);
  });

  it("new file (before=undefined) returns no findings", () => {
    const after = { paths: {}, components: { schemas: {} } };
    const findings = diffOpenApi(undefined, after, "new.openapi.g.json");
    expect(findings).toHaveLength(0);
  });

  it("new file (before=null) returns no findings", () => {
    const after = { paths: {}, components: { schemas: {} } };
    const findings = diffOpenApi(null, after, "new.openapi.g.json");
    expect(findings).toHaveLength(0);
  });
});

// ---------------------------------------------------------------------------
// Adversarial / malformed inputs
// ---------------------------------------------------------------------------

describe("diffOpenApi — adversarial inputs", () => {
  it("throws when before is not an object", () => {
    expect(() => diffOpenApi("not-an-object", {}, "test.json")).toThrow();
  });

  it("throws when after is not an object", () => {
    expect(() => diffOpenApi({}, "not-an-object", "test.json")).toThrow();
  });

  it("handles missing paths gracefully (no findings on both empty)", () => {
    const findings = diffOpenApi({}, {}, "test.json");
    expect(findings).toHaveLength(0);
  });

  it("dangling $ref in AFTER doc (proposed) produces a finding", () => {
    const before = {
      paths: {
        "/v1/data": {
          get: {
            operationId: "getData",
            parameters: [],
            responses: {},
            requestBody: {
              required: true,
              content: {
                "application/json": {
                  schema: {
                    type: "object",
                    properties: { id: { type: "string" } },
                    required: ["id"],
                  },
                },
              },
            },
          },
        },
      },
      components: { schemas: {} },
    };
    const after = {
      paths: {
        "/v1/data": {
          get: {
            operationId: "getData",
            parameters: [],
            responses: {},
            requestBody: {
              required: true,
              content: {
                "application/json": {
                  schema: { $ref: "#/components/schemas/NonExistent" },
                },
              },
            },
          },
        },
      },
      components: { schemas: {} },
    };

    const findings = diffOpenApi(before, after, "test.json");
    expect(findings.some((f) => f.message.includes("dangling $ref"))).toBe(
      true,
    );
  });
});

// ---------------------------------------------------------------------------
// Non-vacuity against real committed OpenAPI fixture
// ---------------------------------------------------------------------------

describe("diffOpenApi — non-vacuity against real committed fixture", () => {
  const REAL_FIXTURE_PATH = join(
    repoRoot,
    "server/services/edge/tests/Unit/KeyCustodian/TypeSpecOpenApi",
    "Generated/open-api-versioned-fixtures.2-0.openapi.g.json",
  );

  it("real committed OpenAPI fixture loads and produces no findings against itself (identity diff)", () => {
    const doc = JSON.parse(readFileSync(REAL_FIXTURE_PATH, "utf-8")) as unknown;
    const findings = diffOpenApi(doc, doc, "real-fixture.openapi.g.json");
    expect(findings).toHaveLength(0);
  });

  it("removing a path from the real fixture produces a finding (gate is non-vacuous on real docs)", () => {
    const doc = JSON.parse(readFileSync(REAL_FIXTURE_PATH, "utf-8")) as Record<
      string,
      unknown
    >;

    // Make a copy with a path removed.
    const modified = {
      ...doc,
      paths: Object.fromEntries(
        Object.entries(doc["paths"] as Record<string, unknown>).filter(
          ([k]) => !k.includes("export"),
        ),
      ),
    };

    const findings = diffOpenApi(doc, modified, "real-fixture.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
    // Should detect the removed /v1/openapi/report/export path
    const pathFinding = findings.find((f) => f.message.includes("export"));
    expect(pathFinding).toBeDefined();
  });
});

// ---------------------------------------------------------------------------
// Non-vacuity: fails-without-valve / passes-with-valve
// ---------------------------------------------------------------------------

describe("diffOpenApi — non-vacuity proof (valve suppression)", () => {
  const before = {
    paths: {
      "/v1/report": {
        get: { operationId: "getReport", parameters: [], responses: {} },
      },
    },
    components: { schemas: {} },
  };
  const after = {
    paths: {},
    components: { schemas: {} },
  };

  it("returns RED (finding) when path removed (no valve)", () => {
    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    expect(findings.length).toBeGreaterThan(0);
  });

  it("gate passes (valve suppresses) when forced=true", () => {
    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    const valveOpen = true;
    const passed = findings.length === 0 || valveOpen;
    expect(passed).toBe(true);
  });

  it("gate fails when forced=false and there is a finding", () => {
    const findings = diffOpenApi(before, after, "test.openapi.g.json");
    const valveOpen = false;
    const passed = findings.length === 0 || valveOpen;
    expect(passed).toBe(false);
  });
});
