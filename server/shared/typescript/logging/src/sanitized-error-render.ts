// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Safe error metadata for logging. The `message` field of an `Error` can
 * carry untrusted strings (broker URIs with embedded credentials, user
 * input echoed back, etc.) — never log it directly. This helper extracts
 * type name + first stack frame, which are operator-meaningful but
 * structurally safe.
 */
export interface SanitizedErrorRender {
  /** Constructor name (e.g. `"TypeError"`, `"AmqpConnectError"`). */
  readonly name: string;
  /** First stack-trace frame (file + line) when available, else absent. */
  readonly firstFrame?: string;
}

/**
 * Extracts the safe shape from an unknown error value. Matches the .NET
 * `SanitizedExceptionRender.TypeName(ex)` + `FirstFrame(ex)` helpers.
 */
export function sanitizedErrorRender(err: unknown): SanitizedErrorRender {
  if (err instanceof Error) {
    const name = err.name || "Error";
    const stack = err.stack ?? "";
    const lines = stack
      .split("\n")
      .map((l) => l.trim())
      .filter(Boolean);
    // Skip the leading "Error: ..." line that Node prepends.
    const frame = lines.find((l) => l.startsWith("at "));
    return { name, firstFrame: frame };
  }
  return { name: typeof err };
}
