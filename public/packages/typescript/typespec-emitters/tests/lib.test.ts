// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Tests for the $lib diagnostics catalog.
//
// Verifies:
//   (a) Every catalog entry has severity "error" (generic guard — mirrors
//       the decorators package lib test pattern).
//   (b) "unmapped-scalar" (D2TSP001) is present with the expected param.

import { describe, it, expect } from "vitest";
import { $lib } from "../src/lib.js";

describe("lib_AllDiagnosticsHaveErrorSeverity", () => {
  it("every entry in $lib.diagnostics has severity 'error'", () => {
    for (const [code, descriptor] of Object.entries($lib.diagnostics)) {
      expect(
        descriptor.severity,
        `diagnostic '${code}' should have severity 'error'`,
      ).toBe("error");
    }
  });
});

describe("lib_UnmappedScalarPresent", () => {
  it("'unmapped-scalar' (D2TSP001) is in the catalog", () => {
    expect($lib.diagnostics["unmapped-scalar"]).toBeDefined();
  });

  it("'unmapped-scalar' messages.default is a paramMessage with 'scalar' param", () => {
    const descriptor = $lib.diagnostics["unmapped-scalar"];
    // The paramMessage function returns a tagged-template-style function.
    // Verify it is callable (the TypeSpec compiler invokes it with named args).
    expect(typeof descriptor.messages.default).toBe("function");
  });

  it("$lib name is '@dcsv-io/d2-typespec-emitters'", () => {
    expect($lib.name).toBe("@dcsv-io/d2-typespec-emitters");
  });
});

describe("lib_MissingCqrsCategoryPresent", () => {
  it("'missing-cqrs-category' (D2TSP003) is in the catalog", () => {
    expect($lib.diagnostics["missing-cqrs-category"]).toBeDefined();
  });

  it("'missing-cqrs-category' has severity 'error'", () => {
    expect($lib.diagnostics["missing-cqrs-category"].severity).toBe("error");
  });

  it("'missing-cqrs-category' messages.default is callable (paramMessage)", () => {
    const descriptor = $lib.diagnostics["missing-cqrs-category"];
    expect(typeof descriptor.messages.default).toBe("function");
  });
});

describe("lib_ServerPushRequiresPayloadPresent", () => {
  it("'server-push-requires-payload' (D2TSP008) is in the catalog", () => {
    expect($lib.diagnostics["server-push-requires-payload"]).toBeDefined();
  });

  it("'server-push-requires-payload' (D2TSP008) has severity 'error'", () => {
    expect($lib.diagnostics["server-push-requires-payload"].severity).toBe(
      "error",
    );
  });

  it("'server-push-requires-payload' messages.default is callable (paramMessage)", () => {
    const descriptor = $lib.diagnostics["server-push-requires-payload"];
    expect(typeof descriptor.messages.default).toBe("function");
  });
});

describe("lib_HostRoutingDiagnosticsPresent", () => {
  const codes = [
    "missing-served-by-for-host-routing",
    "missing-process-kind",
    "unknown-process-kind",
    "missing-routes-namespace",
    "missing-bridge-namespace",
    "standalone-route-requires-grpc",
  ] as const;

  for (const code of codes) {
    it(`'${code}' is in the catalog with severity error`, () => {
      expect($lib.diagnostics[code]).toBeDefined();
      expect($lib.diagnostics[code].severity).toBe("error");
      expect(typeof $lib.diagnostics[code].messages.default).toBe("function");
    });
  }
});
