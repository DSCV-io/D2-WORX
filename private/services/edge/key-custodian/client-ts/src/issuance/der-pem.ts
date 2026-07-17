// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Encode DER bytes as a PEM block with the given label (e.g. `"CERTIFICATE"`,
 * `"PRIVATE KEY"`). Base64 is wrapped at 64 columns per RFC 7468; the block ends
 * with a trailing newline. Pure + synchronous — no crypto, no I/O.
 *
 * The 64-column wrap uses a bounded index loop (no regex) — linear in the input
 * length with no backtracking surface.
 *
 * @param der   - The DER-encoded bytes.
 * @param label - The PEM label placed in the BEGIN/END delimiters.
 * @returns The PEM text (trailing newline included).
 */
export function derToPem(der: Uint8Array, label: string): string {
  const b64 = Buffer.from(der).toString("base64");
  const lines: string[] = [];

  for (let i = 0; i < b64.length; i += 64) lines.push(b64.slice(i, i + 64));

  return `-----BEGIN ${label}-----\n${lines.join("\n")}\n-----END ${label}-----\n`;
}
