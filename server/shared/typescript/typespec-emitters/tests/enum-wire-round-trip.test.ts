// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// The TypeScript half of the cross-language enum-wire parity suite. Drives the
// SAME shared fixture (contracts/enum/enum-parity.fixture.json) as the .NET
// EnumWireRoundTripTests, so an identical wire string resolves to the same
// member identity in both languages.
//
// The TS wire enum is the const-object emitted by the TS DTO emitter: the const
// VALUE is the wire string and the KEY is the C# member identifier. A consumer
// resolves a wire string by reverse-lookup over the const values; an UNKNOWN
// wire string has NO matching value → it is rejected (the const-object has no
// fallback). This mirrors the C# strict fail-loud policy (JsonException / the
// proto mapper's ValidationFailed) — NO silent fallback sentinel.

import { describe, it, expect } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { findRepoRoot } from "./repo-root.js";

// ---------------------------------------------------------------------------
// The committed const-objects (the emitter output shape — value === wire string).
// These mirror what emitTsDtos emits; the byte-parity / integration tests pin
// that the emitter produces exactly these. Here we exercise the WIRE BEHAVIOR.
// ---------------------------------------------------------------------------

const FixtureKeyKind = {
  Rsa: "Rsa",
  Aes: "Aes",
  Secret: "Secret",
} as const;

const FixtureLevel = {
  Low: "Low",
  Medium: "Medium",
  High: "High",
} as const;

const FixtureStatus = {
  Active: "active",
  Inactive: "inactive",
  Pending: "pending",
} as const;

const FixtureAccountKind = {
  Internal: "internal",
  ThirdParty: "third-party",
} as const;

const EnumFixtureWalkOutputInlineState = {
  Draft: "draft",
  Published: "published",
  Archived: "archived",
} as const;

const CONST_OBJECTS: Record<string, Record<string, string>> = {
  FixtureKeyKind,
  FixtureLevel,
  FixtureStatus,
  FixtureAccountKind,
  EnumFixtureWalkOutputInlineState,
};

// ---------------------------------------------------------------------------
// Shared fixture loader
// ---------------------------------------------------------------------------

interface MemberFixture {
  readonly memberName: string;
  readonly wire: string;
}

interface EnumFixture {
  readonly name: string;
  readonly members: readonly MemberFixture[];
  readonly unknownValues: readonly string[];
}

interface FixtureFile {
  readonly schemaVersion: number;
  readonly enums: readonly EnumFixture[];
}

function loadFixture(): FixtureFile {
  // Resolved via sentinel walk-up; tolerates any future folder-depth change.
  const path = join(
    findRepoRoot(import.meta.url),
    "contracts",
    "enum",
    "enum-parity.fixture.json",
  );
  return JSON.parse(readFileSync(path, "utf8")) as FixtureFile;
}

/** Reverse-lookup: a wire string → its member key, or undefined when unknown. */
function memberForWire(
  obj: Record<string, string>,
  wire: string,
): string | undefined {
  for (const [key, value] of Object.entries(obj))
    if (value === wire) return key;

  return undefined;
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("enumWireRoundTrip_SharedFixtureLoads", () => {
  it("the shared fixture is present and every emitted enum has a fixture entry", () => {
    const fx = loadFixture();
    expect(fx.enums.length).toBeGreaterThan(0);

    for (const name of Object.keys(CONST_OBJECTS))
      expect(
        fx.enums.some((e) => e.name === name),
        `fixture must cover ${name}`,
      ).toBe(true);
  });
});

describe("enumWireRoundTrip_KnownWire_ResolvesToMember", () => {
  it("every fixture member's wire string resolves to its member in the const-object", () => {
    const fx = loadFixture();

    for (const en of fx.enums) {
      const obj = CONST_OBJECTS[en.name];
      if (obj === undefined) continue;

      for (const m of en.members) {
        // The const VALUE for the member equals the wire string.
        expect(obj[m.memberName]).toBe(m.wire);
        // Reverse-lookup the wire string → the member key.
        expect(memberForWire(obj, m.wire)).toBe(m.memberName);
      }
    }
  });

  it("S-2 Level value is the member NAME string (matching the C# string wire), not the int", () => {
    expect(FixtureLevel.High).toBe("High");
    expect(FixtureLevel.Low).toBe("Low");
    // The integer backing is C#-side only — it never appears in the TS wire value.
    expect(Object.values(FixtureLevel)).not.toContain(0);
    expect(Object.values(FixtureLevel)).not.toContain(10);
  });

  it("S-3 AccountKind ThirdParty value is the hyphenated literal", () => {
    expect(FixtureAccountKind.ThirdParty).toBe("third-party");
    expect(FixtureAccountKind.Internal).toBe("internal");
  });
});

describe("enumWireRoundTrip_UnknownWire_RejectedNoFallback", () => {
  it("AD-1 (TS half): every fixture unknown value misses const-object membership (no fallback)", () => {
    const fx = loadFixture();

    for (const en of fx.enums) {
      const obj = CONST_OBJECTS[en.name];
      if (obj === undefined) continue;

      for (const unknown of en.unknownValues) {
        // TS reverse-lookup is case-SENSITIVE + exact — an unknown (or wrong-case)
        // wire value has NO member. This is the documented C#-case-insensitive /
        // TS-case-sensitive divergence (e.g. "rsa"/"RSA" map in C#, miss in TS).
        expect(
          memberForWire(obj, unknown),
          `'${unknown}' must NOT resolve to a ${en.name} member`,
        ).toBeUndefined();
      }
    }
  });

  it("a key lookup for a non-member returns undefined (no Unknown sentinel)", () => {
    expect(
      (FixtureKeyKind as Record<string, string>)["Quantum"],
    ).toBeUndefined();
    expect(
      (FixtureStatus as Record<string, string>)["deleted"],
    ).toBeUndefined();
  });
});

describe("enumWireRoundTrip_CrossLanguageParity", () => {
  it("the const-object values are EXACTLY the fixture wire strings (same as the C# wire)", () => {
    const fx = loadFixture();

    for (const en of fx.enums) {
      const obj = CONST_OBJECTS[en.name];
      if (obj === undefined) continue;

      const fixtureWires = en.members.map((m) => m.wire).sort();
      const constValues = Object.values(obj).sort();
      expect(constValues).toEqual(fixtureWires);
    }
  });
});
