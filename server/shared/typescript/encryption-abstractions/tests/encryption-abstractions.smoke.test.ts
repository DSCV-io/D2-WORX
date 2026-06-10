// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import {
  ALL_ENCRYPTION_DOMAINS,
  ALL_ENCRYPTION_FRAME_FIELDS,
  EncryptionDomains,
  EncryptionFrame,
} from "../src/index.js";

// The four closed domain identifiers — spec source of truth is
// contracts/encryption-domains/encryption-domains.spec.json.
// Mirrors .NET D2.Shared.Encryption.EncryptionDomains wire values.
const EXPECTED_DOMAINS = ["audit", "notifications", "courier", "plaintext"] as const;

// The five logical frame fields declared in the spec (variable-length fields
// are represented by the field name only, without offset/length constants).
const EXPECTED_FRAME_FIELDS = [
  "VERSION",
  "KID_LENGTH",
  "KID",
  "NONCE",
  "CIPHERTEXT_WITH_TAG",
] as const;

describe("@d2/encryption-abstractions — EncryptionDomains", () => {
  it("ALL_ENCRYPTION_DOMAINS contains exactly the four spec-declared identifiers", () => {
    expect([...ALL_ENCRYPTION_DOMAINS].sort()).toEqual(
      [...EXPECTED_DOMAINS].sort(),
    );
  });

  it("ALL_ENCRYPTION_DOMAINS has no duplicates", () => {
    expect(new Set(ALL_ENCRYPTION_DOMAINS).size).toBe(
      ALL_ENCRYPTION_DOMAINS.length,
    );
  });

  it("EncryptionDomains constants match ALL_ENCRYPTION_DOMAINS", () => {
    const catalogSet = new Set<string>(ALL_ENCRYPTION_DOMAINS);
    for (const value of Object.values(EncryptionDomains))
      expect(catalogSet.has(value)).toBe(true);
    expect(Object.values(EncryptionDomains)).toHaveLength(
      ALL_ENCRYPTION_DOMAINS.length,
    );
  });

  it("PLAINTEXT sentinel is present so callers can distinguish no-encryption from a real domain", () => {
    expect(EncryptionDomains.PLAINTEXT).toBe("plaintext");
    expect(ALL_ENCRYPTION_DOMAINS).toContain("plaintext");
  });
});

describe("@d2/encryption-abstractions — EncryptionFrame", () => {
  it("ALL_ENCRYPTION_FRAME_FIELDS contains exactly the five spec-declared field names", () => {
    expect([...ALL_ENCRYPTION_FRAME_FIELDS].sort()).toEqual(
      [...EXPECTED_FRAME_FIELDS].sort(),
    );
  });

  it("frame layout constants have sane byte values (mirrors .NET EncryptionFrameLayout)", () => {
    expect(EncryptionFrame.CURRENT_VERSION).toBe(1);
    expect(EncryptionFrame.VERSION_OFFSET).toBe(0);
    expect(EncryptionFrame.VERSION_LENGTH).toBe(1);
    expect(EncryptionFrame.KID_LENGTH_OFFSET).toBe(1);
    expect(EncryptionFrame.KID_OFFSET).toBe(2);
    expect(EncryptionFrame.NONCE_LENGTH).toBe(12);
    expect(EncryptionFrame.CONSTRAINT_TAG_LENGTH).toBe(16);
  });

  it("CONSTRAINT_MIN_FRAME_SIZE pins the spec-declared minimum of 30 bytes", () => {
    // Spec-declared: VERSION(1) + KID_LENGTH(1) + KID_min(1) + NONCE(12) + TAG(16) =
    // 31 theoretical minimum with a 1-byte KID, but the spec uses 30 as the floor
    // (0-byte KID edge, handled by the decoder's kid-length validation separately).
    // This test pins the cross-runtime constant so a spec bump surfaces immediately.
    expect(EncryptionFrame.CONSTRAINT_MIN_FRAME_SIZE).toBe(30);
    // Sanity: the floor is large enough to hold the fixed-size fields alone
    // (VERSION + KID_LENGTH + NONCE + TAG = 1 + 1 + 12 + 16 = 30).
    const fixedFieldsSize =
      EncryptionFrame.VERSION_LENGTH +
      EncryptionFrame.KID_LENGTH_LENGTH +
      EncryptionFrame.NONCE_LENGTH +
      EncryptionFrame.CONSTRAINT_TAG_LENGTH;
    expect(EncryptionFrame.CONSTRAINT_MIN_FRAME_SIZE).toBe(fixedFieldsSize);
  });
});
