// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

import { describe, expect, it } from "vitest";

import { type ContextSpec, emitAuthContext } from "../src/auth-context-emit.js";
import {
  emitPropagatedContextInterface,
  emitPropagatedSerializer,
} from "../src/request-context-emit.js";

// ---------------------------------------------------------------------------
// Byte-parity golden test: regenerate the request-context `.g.ts` files
// IN-MEMORY from the real contracts/request-context/IRequestContext.spec.json
// and assert each equals the committed bytes (LF-normalized). Each `.g.ts`
// carries a deliberate-drift proof (mutate a spec input → output changes) so
// the byte-compare is non-vacuous. Mirrors
// tools/ts-codegen/tests/error-codes-byte-parity.test.ts.
//
// The interface (IRequestContext.g.ts) is emitted by emitAuthContext in
// importMode="package" — exactly as runRequestContextEmit writes it.
// ---------------------------------------------------------------------------

const here = dirname(fileURLToPath(import.meta.url));
const repoRoot = resolve(here, "..", "..", "..");

function readJson<T>(...parts: string[]): T {
  return JSON.parse(readFileSync(resolve(repoRoot, ...parts), "utf8")) as T;
}

function readGenerated(...parts: string[]): string {
  // LF-normalize so a checkout CRLF setting can't produce a spurious mismatch.
  return readFileSync(resolve(repoRoot, ...parts), "utf8").replace(
    /\r\n/g,
    "\n",
  );
}

const spec = readJson<ContextSpec>(
  "contracts",
  "request-context",
  "IRequestContext.spec.json",
);

const RC_SRC = [
  "server",
  "shared",
  "typescript",
  "request-context-abstractions",
  "src",
];

describe("request-context byte-parity (in-memory regen == committed .g.ts)", () => {
  it("IRequestContext.g.ts is byte-identical to committed", () => {
    const r = emitAuthContext(spec, "package");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).toBe(readGenerated(...RC_SRC, "IRequestContext.g.ts"));
  });

  it("IPropagatedContext.g.ts is byte-identical to committed", () => {
    expect(emitPropagatedContextInterface(spec)).toBe(
      readGenerated(...RC_SRC, "IPropagatedContext.g.ts"),
    );
  });

  it("PropagatedContextSerializer.g.ts is byte-identical to committed", () => {
    expect(emitPropagatedSerializer(spec)).toBe(
      readGenerated(...RC_SRC, "PropagatedContextSerializer.g.ts"),
    );
  });

  it("deliberate-drift proof: an added property changes the IRequestContext interface", () => {
    const committed = readGenerated(...RC_SRC, "IRequestContext.g.ts");
    const drifted: ContextSpec = {
      ...spec,
      sections: [
        ...spec.sections,
        {
          name: "Drift",
          properties: [
            { name: "DriftField", type: "string?", doc: "Deliberate drift." },
          ],
        },
      ],
    };
    const r = emitAuthContext(drifted, "package");
    expect(r.diagnostics).toEqual([]);
    expect(r.source).not.toBe(committed);
  });

  it("deliberate-drift proof: an added propagated field changes IPropagatedContext", () => {
    const committed = readGenerated(...RC_SRC, "IPropagatedContext.g.ts");
    const drifted: ContextSpec = {
      ...spec,
      sections: [
        ...spec.sections,
        {
          name: "Drift",
          properties: [
            {
              name: "DriftField",
              type: "string?",
              propagate: true,
              doc: "Deliberate drift.",
            },
          ],
        },
      ],
    };
    expect(emitPropagatedContextInterface(drifted)).not.toBe(committed);
  });

  it("deliberate-drift proof: mutating a propagated maxLength changes the serializer", () => {
    const committed = readGenerated(
      ...RC_SRC,
      "PropagatedContextSerializer.g.ts",
    );
    const drifted: ContextSpec = {
      ...spec,
      sections: spec.sections.map((section) => ({
        ...section,
        properties: section.properties.map((p) =>
          p.maxLength !== undefined ? { ...p, maxLength: p.maxLength + 7 } : p,
        ),
      })),
    };
    expect(emitPropagatedSerializer(drifted)).not.toBe(committed);
  });
});
