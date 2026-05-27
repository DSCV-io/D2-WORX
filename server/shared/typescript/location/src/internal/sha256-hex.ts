// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { createHash } from "node:crypto";

/**
 * Computes SHA-256 hex digest of a UTF-8-encoded string. One-shot — no
 * stream / no leak. Mirrors .NET `SHA256.HashData(Encoding.UTF8.GetBytes(input))`
 * + `Convert.ToHexStringLower(...)`.
 */
export function sha256Hex(input: string): string {
  return createHash("sha256").update(input, "utf8").digest("hex");
}
