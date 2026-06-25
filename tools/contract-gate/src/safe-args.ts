// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Git argument safety guards — allowlist-validated ref + path validators.
//
// These guards are called at the point of resolution, before any git or buf
// subprocess invocation, so untrusted CLI-supplied values never reach a shell.
//
// Design notes:
//   - `validateGitRef` uses a strict character-class allowlist: alphanumerics,
//     `.`, `_`, `/`, `-` only. Leading `-` is explicitly rejected (arg-injection
//     guard). `..` sequences are rejected (ref-trickery + traversal). A plain
//     40- or 64-hex SHA is also accepted.
//   - `validateGitPath` rejects `..` segments (directory traversal) and absolute
//     paths that escape the repo root.
//   - Both throw on violation — no silent truncation or fallback (strict,
//     fail-loud per project convention).
//   - No `@d2/utilities` dependency: this module is imported by release-runner
//     via the contract-gate public surface, and adding a new cross-package
//     runtime dep would require a pnpm install cycle. Plain JS guards are
//     self-contained and testable with no external deps.

// ---------------------------------------------------------------------------
// validateGitRef
// ---------------------------------------------------------------------------

/**
 * Strictly validates that `ref` is a safe git ref before it is interpolated
 * into a git or buf subprocess invocation.
 *
 * Accepts:
 *   - Branch / tag names composed of `[A-Za-z0-9._/-]`
 *     (e.g. `nova`, `main`, `origin/main`, `feature/x-y`, `v2.1.0`)
 *   - 40-hex commit SHAs (e.g. `a3f4b1c2d5...`)
 *   - 64-hex SHAs
 *
 * Rejects:
 *   - Empty string
 *   - Leading `-` (git arg-injection: `--upload-pack=...`, `-x`)
 *   - `..` anywhere (traversal + rev-range trickery)
 *   - Shell metacharacters: `; | & $ \` ( ) < > ' " \ # ! * ? ~ space \t \n`
 *
 * @param ref - The git ref string to validate.
 * @throws {Error} When `ref` contains disallowed characters or patterns.
 */
export function validateGitRef(ref: string): void {
  if (ref.length === 0) {
    throw new Error("git ref validation failed: ref must not be empty");
  }

  // Reject leading `-` — any leading dash is a git flag injection.
  if (ref.startsWith("-")) {
    throw new Error(
      `git ref validation failed: ref must not start with '-' (got: ${JSON.stringify(ref)})`,
    );
  }

  // Reject `..` anywhere — rev-range injection / path traversal.
  if (ref.includes("..")) {
    throw new Error(
      `git ref validation failed: ref must not contain '..' (got: ${JSON.stringify(ref)})`,
    );
  }

  // Reject shell metacharacters and whitespace.
  // Allowlist: A-Z a-z 0-9 . _ / -
  // Everything else is a violation.
  const _ALLOWLIST_RE = /^[A-Za-z0-9._/-]+$/;

  if (!_ALLOWLIST_RE.test(ref)) {
    throw new Error(
      "git ref validation failed: ref contains disallowed characters" +
        ` — only [A-Za-z0-9._/-] are permitted (got: ${JSON.stringify(ref)})`,
    );
  }
}

// ---------------------------------------------------------------------------
// validateGitPath
// ---------------------------------------------------------------------------

/**
 * Strictly validates that `filePath` is a safe relative repo path before it
 * is appended to a `git show <ref>:<path>` invocation.
 *
 * Accepts:
 *   - Normal relative paths: `contracts/messages/en.json`, `foo/bar.proto`
 *
 * Rejects:
 *   - Empty string
 *   - Absolute paths (starting with `/`, `\`, or a drive letter `C:\`)
 *   - Any path segment that is exactly `..` (directory traversal)
 *
 * @param filePath - The file path string to validate.
 * @throws {Error} When `filePath` contains traversal or absolute-escape patterns.
 */
export function validateGitPath(filePath: string): void {
  if (filePath.length === 0) {
    throw new Error("git path validation failed: filePath must not be empty");
  }

  // Reject absolute paths — starts with `/`, `\`, or Windows drive `C:\`/`C:/`.
  if (
    filePath.startsWith("/") ||
    filePath.startsWith("\\") ||
    /^[A-Za-z]:[/\\]/.test(filePath)
  ) {
    throw new Error(
      "git path validation failed: filePath must be relative, not absolute" +
        ` (got: ${JSON.stringify(filePath)})`,
    );
  }

  // Reject `..` path segments — directory traversal.
  // Normalise to forward slashes first so `foo\..\bar` is caught on Windows too.
  const normalised = filePath.replace(/\\/g, "/");
  const segments = normalised.split("/");

  for (const seg of segments) {
    if (seg === "..") {
      throw new Error(
        "git path validation failed: filePath must not contain '..' segments" +
          ` (got: ${JSON.stringify(filePath)})`,
      );
    }
  }
}
