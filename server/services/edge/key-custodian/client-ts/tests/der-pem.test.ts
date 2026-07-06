// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { derToPem } from "../src/der-pem.js";

describe("derToPem", () => {
  it("wraps a small DER in a labeled PEM block with a trailing newline", () => {
    const der = new Uint8Array([1, 2, 3, 4, 5]);
    const pem = derToPem(der, "CERTIFICATE");

    expect(pem.startsWith("-----BEGIN CERTIFICATE-----\n")).toBe(true);
    expect(pem.endsWith("-----END CERTIFICATE-----\n")).toBe(true);
    expect(pem).toContain(Buffer.from(der).toString("base64"));
  });

  it("wraps base64 at 64 columns", () => {
    // 240 bytes → 320 base64 chars → 5 lines (64,64,64,64,64).
    const der = new Uint8Array(240).fill(0xab);
    const pem = derToPem(der, "PRIVATE KEY");
    const body = pem
      .replace("-----BEGIN PRIVATE KEY-----\n", "")
      .replace("\n-----END PRIVATE KEY-----\n", "");
    const lines = body.split("\n");

    expect(lines).toHaveLength(5);
    for (const line of lines) expect(line.length).toBeLessThanOrEqual(64);
    expect(lines[0]!.length).toBe(64);
  });

  it("round-trips through base64 losslessly", () => {
    const der = new Uint8Array([0, 255, 128, 64, 32, 16, 8, 4, 2, 1]);
    const pem = derToPem(der, "CERTIFICATE");
    const b64 = pem
      .split("\n")
      .filter((l) => !l.startsWith("-----"))
      .join("");

    expect(new Uint8Array(Buffer.from(b64, "base64"))).toEqual(der);
  });

  it("handles an empty DER (single empty body line)", () => {
    const pem = derToPem(new Uint8Array(0), "CERTIFICATE");

    expect(pem).toBe(
      "-----BEGIN CERTIFICATE-----\n\n-----END CERTIFICATE-----\n",
    );
  });
});
