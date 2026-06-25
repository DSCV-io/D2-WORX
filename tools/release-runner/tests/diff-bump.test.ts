// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";

import {
  deriveBump,
  type ApiDiff,
  type BreakingFooter,
  type FingerprintDiff,
} from "../src/diff-bump.js";

// ---------------------------------------------------------------------------
// Fixture helpers
// ---------------------------------------------------------------------------

function noApi(): ApiDiff {
  return { added: false, removed: false, changed: false };
}

function addedApi(): ApiDiff {
  return { added: true, removed: false, changed: false };
}

function removedApi(): ApiDiff {
  return { added: false, removed: true, changed: false };
}

function changedApi(): ApiDiff {
  return { added: false, removed: false, changed: true };
}

function fpSame(): FingerprintDiff {
  return { changed: false };
}

function fpChanged(): FingerprintDiff {
  return { changed: true };
}

function noFooter(): BreakingFooter {
  return { forced: false, wireBreaking: [], apiBreaking: [] };
}

function forcedFooter(): BreakingFooter {
  return {
    forced: true,
    wireBreaking: ["Remove legacy field"],
    apiBreaking: [],
  };
}

function apiForcedFooter(): BreakingFooter {
  return {
    forced: true,
    wireBreaking: [],
    apiBreaking: ["Renamed GetFoo to GetBar"],
  };
}

// ---------------------------------------------------------------------------
// Transition matrix — pure derivation (no IO)
// ---------------------------------------------------------------------------

describe("deriveBump — transition matrix", () => {
  // Row 1: all-false diff, no footer → none
  it("P1: output identical, no footer → none", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("none");
  });

  // Row 2: fingerprint changed, API unchanged, no footer → patch
  it("P2: fingerprint changed only (API surface identical, no footer) → patch", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("patch");
  });

  // Row 3: API added only, no footer → minor
  it("P3: api added only (no removal, no fingerprint change) → minor", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Row 3b: API added + fingerprint changed → still minor (added wins over patch)
  it("P3b: api added with fingerprint changed → minor (added > patch)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Row 4: API removed, stable ≥1.0.0 → major
  it("P4: api removed, stable (1.2.0) → major", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  // Row 5: API removed, pre-stable 0.x → minor (carve-out)
  it("P5: api removed, pre-stable 0.x → minor (pre-stable carve-out)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "0.4.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Row 6: API changed (signature), stable → major
  it("P6: api changed (signature), stable (1.2.0) → major", () => {
    expect(
      deriveBump({
        apiDiff: changedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  // Row 7: API changed (signature), pre-stable → minor
  it("P7: api changed (signature), pre-stable (0.4.0) → minor (pre-stable carve-out)", () => {
    expect(
      deriveBump({
        apiDiff: changedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "0.4.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Row 8: footer forced + output identical, stable → major (footer authoritative)
  it("P8: footer forced + output identical (no diff), stable → major (footer escalates none→major)", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "1.2.0",
        footer: forcedFooter(),
      }),
    ).toBe("major");
  });

  // Row 9: footer forced + output identical, pre-stable → minor (footer escalates but carve-out applies)
  it("P9: footer forced + output identical, pre-stable (0.4.0) → minor (escalation capped at minor)", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "0.4.0",
        footer: forcedFooter(),
      }),
    ).toBe("minor");
  });

  // Row 10: footer forced + api added, stable → major (footer escalates minor→major)
  it("P10: footer forced + api added, stable → major (footer escalates minor→major)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: forcedFooter(),
      }),
    ).toBe("major");
  });

  // Row 11: fingerprint changed + footer forced, stable → major (footer escalates patch→major)
  it("P11: fingerprint changed + footer forced, stable → major (footer escalates patch→major)", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: forcedFooter(),
      }),
    ).toBe("major");
  });

  // Row 12: api added, pre-stable 0.x → minor (added is minor regardless of pre-stable)
  it("P12: api added, pre-stable (0.4.0) → minor (added is always minor; carve-out only applies to breaks)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "0.4.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });
});

// ---------------------------------------------------------------------------
// Pre-stable carve-out — prerelease label (e.g. 1.0.0-alpha.3)
// ---------------------------------------------------------------------------

describe("deriveBump — prerelease label treated as pre-stable", () => {
  // Row P7 variant: api changed, version carries a prerelease label → minor
  it("api changed, 1.0.0-alpha.3 (prerelease label) → minor (pre-stable via label)", () => {
    expect(
      deriveBump({
        apiDiff: changedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.0.0-alpha.3",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  it("api removed, 2.0.0-beta.1 (prerelease label, MAJOR≥1) → minor (pre-stable via label)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "2.0.0-beta.1",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  it("footer forced, 1.0.0-rc.1 (prerelease label) → minor (escalation capped at minor)", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "1.0.0-rc.1",
        footer: forcedFooter(),
      }),
    ).toBe("minor");
  });

  // A version WITHOUT a prerelease label at MAJOR≥1 is stable even if MINOR=0
  it("api removed, 1.0.0 (no prerelease, MAJOR=1) → major (stable)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.0.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });
});

// ---------------------------------------------------------------------------
// Adversarial — combined signals, footer interactions
// ---------------------------------------------------------------------------

describe("deriveBump — adversarial cases", () => {
  // removed AND added together → break wins (not just minor)
  it("api removed+added simultaneously (rename), stable → major (break wins over minor)", () => {
    expect(
      deriveBump({
        apiDiff: { added: true, removed: true, changed: false },
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  it("api removed+added simultaneously (rename), pre-stable → minor (carve-out)", () => {
    expect(
      deriveBump({
        apiDiff: { added: true, removed: true, changed: false },
        fingerprintDiff: fpChanged(),
        currentVersion: "0.3.0",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Footer forced does NOT lower an already-major diff
  it("api removed, stable + footer forced → major (footer cannot lower an existing major)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: forcedFooter(),
      }),
    ).toBe("major");
  });

  // Footer forced, pre-stable, footer forced caps at minor
  it("footer forced + api added, pre-stable (0.x) → minor (both escalations capped at minor)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "0.4.0",
        footer: forcedFooter(),
      }),
    ).toBe("minor");
  });

  // No footer (forced=false, but entries populated — should not happen in practice; forced governs)
  it("footer.forced=false with populated wire/api entries → entries ignored; fingerprint-only → patch", () => {
    const unfiredFooter: BreakingFooter = {
      forced: false,
      wireBreaking: ["something"],
      apiBreaking: ["something else"],
    };

    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.2.0",
        footer: unfiredFooter,
      }),
    ).toBe("patch");
  });

  // All three diff flags simultaneously + footer forced → major (stable)
  it("all api flags + fingerprint changed + footer forced, stable → major", () => {
    expect(
      deriveBump({
        apiDiff: { added: true, removed: true, changed: true },
        fingerprintDiff: fpChanged(),
        currentVersion: "1.0.0",
        footer: forcedFooter(),
      }),
    ).toBe("major");
  });

  // All three diff flags + footer forced, pre-stable → minor (carve-out)
  it("all api flags + fingerprint changed + footer forced, pre-stable → minor", () => {
    expect(
      deriveBump({
        apiDiff: { added: true, removed: true, changed: true },
        fingerprintDiff: fpChanged(),
        currentVersion: "0.1.0",
        footer: forcedFooter(),
      }),
    ).toBe("minor");
  });

  // api added + fingerprint same + footer forced, stable → major (footer escalates minor)
  it("api added + fp same + footer forced, stable → major (minor escalated to major by footer)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "2.5.3",
        footer: apiForcedFooter(),
      }),
    ).toBe("major");
  });

  // No diff at all, no footer, pre-stable → none
  it("nothing changed, pre-stable → none", () => {
    expect(
      deriveBump({
        apiDiff: noApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "0.1.0",
        footer: noFooter(),
      }),
    ).toBe("none");
  });

  // changed-only API, no fingerprint change, stable → major (changed is a break even if fingerprint bytes somehow same)
  it("api changed only (no fp change), stable → major (api change is a break regardless of fingerprint)", () => {
    expect(
      deriveBump({
        apiDiff: changedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "1.0.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  // version "0.0.1" (both MAJOR and MINOR are 0) → pre-stable
  it("api removed, version 0.0.1 → minor (pre-stable MAJOR=0)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "0.0.1",
        footer: noFooter(),
      }),
    ).toBe("minor");
  });

  // Exact boundary: version "1.0.0" (first stable) → major on break
  it("api removed, exact boundary 1.0.0 → major (MAJOR=1 is stable)", () => {
    expect(
      deriveBump({
        apiDiff: removedApi(),
        fingerprintDiff: fpChanged(),
        currentVersion: "1.0.0",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  // Large stable version (10.5.2) → major on break
  it("api changed, large stable version 10.5.2 → major", () => {
    expect(
      deriveBump({
        apiDiff: changedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "10.5.2",
        footer: noFooter(),
      }),
    ).toBe("major");
  });

  // footer forced + api added + pre-stable, both escalate to break — still capped at minor
  it("footer forced + api added, pre-stable (0.9.9) → minor (capped; break+minor both pre-stable)", () => {
    expect(
      deriveBump({
        apiDiff: addedApi(),
        fingerprintDiff: fpSame(),
        currentVersion: "0.9.9",
        footer: forcedFooter(),
      }),
    ).toBe("minor");
  });
});
