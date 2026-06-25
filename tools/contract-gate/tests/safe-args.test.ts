// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Adversarial unit tests for validateGitRef and validateGitPath.
//
// Covers:
//   - Legitimate refs that must be ACCEPTED (no throw).
//   - Injection / traversal / whitespace payloads that must be REJECTED (throw).
//   - Legitimate relative paths that must be ACCEPTED.
//   - Traversal / absolute paths that must be REJECTED.

import { describe, expect, it } from "vitest";
import { validateGitRef, validateGitPath } from "../src/safe-args.js";

// ---------------------------------------------------------------------------
// validateGitRef — ACCEPTED (valid refs must not throw)
// ---------------------------------------------------------------------------

describe("validateGitRef — accepts legitimate refs", () => {
  it("accepts a simple branch name: nova", () => {
    expect(() => validateGitRef("nova")).not.toThrow();
  });

  it("accepts: main", () => {
    expect(() => validateGitRef("main")).not.toThrow();
  });

  it("accepts a remote-tracking ref: origin/main", () => {
    expect(() => validateGitRef("origin/main")).not.toThrow();
  });

  it("accepts a feature branch with hyphens: feature/x-y", () => {
    expect(() => validateGitRef("feature/x-y")).not.toThrow();
  });

  it("accepts a semver tag: v2.1.0", () => {
    expect(() => validateGitRef("v2.1.0")).not.toThrow();
  });

  it("accepts a 40-hex commit SHA", () => {
    expect(() =>
      validateGitRef("a3f4b1c2d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0"),
    ).not.toThrow();
  });

  it("accepts a 64-hex commit SHA", () => {
    expect(() =>
      validateGitRef(
        "a3f4b1c2d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7c8d9e0f1a2",
      ),
    ).not.toThrow();
  });

  it("accepts HEAD", () => {
    expect(() => validateGitRef("HEAD")).not.toThrow();
  });

  it("accepts underscore in name: my_branch", () => {
    expect(() => validateGitRef("my_branch")).not.toThrow();
  });

  it("accepts n/contract-versioning style branch", () => {
    expect(() => validateGitRef("n/contract-versioning")).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// validateGitRef — REJECTED (injection / traversal / bad input must throw)
// ---------------------------------------------------------------------------

describe("validateGitRef — rejects injection and traversal payloads", () => {
  it("rejects empty string", () => {
    expect(() => validateGitRef("")).toThrow(/must not be empty/);
  });

  it("rejects leading dash: -x (arg-injection)", () => {
    expect(() => validateGitRef("-x")).toThrow(/must not start with '-'/);
  });

  it("rejects --upload-pack=x (git arg-injection)", () => {
    expect(() => validateGitRef("--upload-pack=x")).toThrow(
      /must not start with '-'/,
    );
  });

  it("rejects semicolon injection: ; rm -rf /", () => {
    expect(() => validateGitRef("; rm -rf /")).toThrow(
      /disallowed characters|must not start with '-'|must not be empty/,
    );
  });

  it("rejects shell command substitution: $(whoami)", () => {
    expect(() => validateGitRef("$(whoami)")).toThrow(/disallowed characters/);
  });

  it("rejects backtick substitution: `id`", () => {
    expect(() => validateGitRef("`id`")).toThrow(/disallowed characters/);
  });

  it("rejects whitespace in ref: 'a b'", () => {
    expect(() => validateGitRef("a b")).toThrow(/disallowed characters/);
  });

  it("rejects dot-dot ref traversal: ..", () => {
    expect(() => validateGitRef("..")).toThrow(/must not contain '\.\.'/);
  });

  it("rejects dot-dot in path-like ref: a/../../etc", () => {
    expect(() => validateGitRef("a/../../etc")).toThrow(
      /must not contain '\.\.'/,
    );
  });

  it("rejects pipe metacharacter: a|b", () => {
    expect(() => validateGitRef("a|b")).toThrow(/disallowed characters/);
  });

  it("rejects ampersand metacharacter: a&b", () => {
    expect(() => validateGitRef("a&b")).toThrow(/disallowed characters/);
  });

  it("rejects hash metacharacter: a#b", () => {
    expect(() => validateGitRef("a#b")).toThrow(/disallowed characters/);
  });

  it("rejects dollar sign: $VAR", () => {
    expect(() => validateGitRef("$VAR")).toThrow(/disallowed characters/);
  });

  it("rejects newline in ref", () => {
    expect(() => validateGitRef("main\nrm -rf")).toThrow(
      /disallowed characters/,
    );
  });

  it("rejects tab character in ref", () => {
    expect(() => validateGitRef("main\tother")).toThrow(
      /disallowed characters/,
    );
  });

  it("rejects single quote: a'b", () => {
    expect(() => validateGitRef("a'b")).toThrow(/disallowed characters/);
  });

  it('rejects double quote: a"b', () => {
    expect(() => validateGitRef('a"b')).toThrow(/disallowed characters/);
  });

  it("rejects backslash: a\\b", () => {
    expect(() => validateGitRef("a\\b")).toThrow(/disallowed characters/);
  });

  it("rejects angle bracket: a<b", () => {
    expect(() => validateGitRef("a<b")).toThrow(/disallowed characters/);
  });

  it("rejects angle bracket: a>b", () => {
    expect(() => validateGitRef("a>b")).toThrow(/disallowed characters/);
  });

  it("rejects bang: a!b", () => {
    expect(() => validateGitRef("a!b")).toThrow(/disallowed characters/);
  });

  it("rejects tilde: a~b", () => {
    expect(() => validateGitRef("a~b")).toThrow(/disallowed characters/);
  });

  it("rejects star glob: a*b", () => {
    expect(() => validateGitRef("a*b")).toThrow(/disallowed characters/);
  });

  it("rejects question mark glob: a?b", () => {
    expect(() => validateGitRef("a?b")).toThrow(/disallowed characters/);
  });
});

// ---------------------------------------------------------------------------
// validateGitPath — ACCEPTED (valid relative paths must not throw)
// ---------------------------------------------------------------------------

describe("validateGitPath — accepts legitimate relative paths", () => {
  it("accepts a normal relative path: contracts/messages/en.json", () => {
    expect(() => validateGitPath("contracts/messages/en.json")).not.toThrow();
  });

  it("accepts a simple filename: foo.proto", () => {
    expect(() => validateGitPath("foo.proto")).not.toThrow();
  });

  it("accepts a nested path: server/shared/dotnet/geo/README.md", () => {
    expect(() =>
      validateGitPath("server/shared/dotnet/geo/README.md"),
    ).not.toThrow();
  });

  it("accepts a spec file path: contracts/geo/geo-error-codes.spec.json", () => {
    expect(() =>
      validateGitPath("contracts/geo/geo-error-codes.spec.json"),
    ).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// validateGitPath — REJECTED (traversal / absolute paths must throw)
// ---------------------------------------------------------------------------

describe("validateGitPath — rejects traversal and absolute paths", () => {
  it("rejects empty string", () => {
    expect(() => validateGitPath("")).toThrow(/must not be empty/);
  });

  it("rejects leading dot-dot: ../x", () => {
    expect(() => validateGitPath("../x")).toThrow(
      /must not contain '\.\.' segments/,
    );
  });

  it("rejects middle dot-dot: a/../../b", () => {
    expect(() => validateGitPath("a/../../b")).toThrow(
      /must not contain '\.\.' segments/,
    );
  });

  it("rejects /etc/passwd (Unix absolute)", () => {
    expect(() => validateGitPath("/etc/passwd")).toThrow(/must be relative/);
  });

  it("rejects \\Windows\\System32 (Windows backslash absolute)", () => {
    expect(() => validateGitPath("\\Windows\\System32")).toThrow(
      /must be relative/,
    );
  });

  it("rejects C:\\secrets\\key.pem (Windows drive absolute)", () => {
    expect(() => validateGitPath("C:\\secrets\\key.pem")).toThrow(
      /must be relative/,
    );
  });

  it("rejects C:/secrets/key.pem (Windows drive forward-slash absolute)", () => {
    expect(() => validateGitPath("C:/secrets/key.pem")).toThrow(
      /must be relative/,
    );
  });

  it("rejects backslash traversal: foo\\..\\bar (normalised to foo/../bar)", () => {
    expect(() => validateGitPath("foo\\..\\bar")).toThrow(
      /must not contain '\.\.' segments/,
    );
  });
});
