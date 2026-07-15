// Copyright (c) DCSV. All rights reserved.
//
// Identity + behavior pin for the shared inspectcode Text-format finding-line
// counter (tools/scripts/count-inspectcode-findings.sh). §26.24: the local
// gate (gates.sh) and COMMANDS.md must not re-inline divergent parse logic —
// they call / cite this script. PR CI does not run inspectcode. Runnable with:
//   node --test tools/scripts/tests/count-inspectcode-findings.test.mjs
//
// On Windows the system `bash` is often the WSL launcher (broken without a
// distro). Prefer Git for Windows bash when present so the behavioral suite
// runs under the same shell family CI uses (ubuntu bash / Git bash).

import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import {
  existsSync,
  mkdtempSync,
  writeFileSync,
  rmSync,
  readFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join, dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { test } from "node:test";

const __dirname = dirname(fileURLToPath(import.meta.url));

/**
 * Walk up from `startDir` until a directory containing the repo-root sentinel
 * (`.git` or `D2.slnx`) is found. Depth-invariant — no hardcoded
 * `../../../` relative walk-up (contrast fixed-depth resolve, which breaks if
 * this test file moves).
 * @param {string} startDir
 * @returns {string}
 */
function findRepoRoot(startDir) {
  let current = resolve(startDir);

  while (true) {
    if (
      existsSync(join(current, ".git")) ||
      existsSync(join(current, "server", "D2.slnx"))
    ) {
      return current;
    }

    const parent = dirname(current);

    if (parent === current) {
      throw new Error(
        `repo-root sentinel: no .git or D2.slnx found walking up from ${startDir}`,
      );
    }

    current = parent;
  }
}

const REPO_ROOT = findRepoRoot(__dirname);
const SCRIPT_REL = "tools/scripts/count-inspectcode-findings.sh";
const SCRIPT_ABS = join(REPO_ROOT, SCRIPT_REL);

/**
 * Resolve a real bash binary. Prefer Git for Windows over the Windows WSL
 * shim at System32/bash.exe when that shim cannot exec a distro.
 * @returns {string}
 */
function resolveBash() {
  const candidates = [
    "C:\\Program Files\\Git\\bin\\bash.exe",
    "C:\\Program Files\\Git\\usr\\bin\\bash.exe",
    process.env.GIT_BASH,
  ].filter((p) => typeof p === "string" && p.length > 0 && existsSync(p));

  for (const candidate of candidates) {
    const probe = spawnSync(candidate, ["-c", "echo ok"], {
      encoding: "utf-8",
    });

    if (probe.status === 0 && (probe.stdout ?? "").includes("ok")) {
      return candidate;
    }
  }

  // Last resort: PATH `bash` (Linux/macOS CI, or a working WSL).
  const pathProbe = spawnSync("bash", ["-c", "echo ok"], { encoding: "utf-8" });

  if (pathProbe.status === 0 && (pathProbe.stdout ?? "").includes("ok")) {
    return "bash";
  }

  throw new Error(
    "No working bash found for count-inspectcode-findings tests. " +
      "Install Git for Windows or set GIT_BASH to a bash.exe path.",
  );
}

const BASH = resolveBash();

test("findRepoRoot is depth-invariant (sentinel, not fixed ../../..)", () => {
  // Happy path: walk from this nested tests/ dir finds the real monorepo root.
  const fromNested = findRepoRoot(__dirname);
  assert.equal(fromNested, REPO_ROOT);
  assert.ok(
    existsSync(join(fromNested, "server", "D2.slnx")),
    "sentinel walk must land on a directory that owns D2.slnx",
  );
  assert.ok(
    existsSync(join(fromNested, SCRIPT_REL)),
    "sentinel walk must land where the shared inspectcode script lives",
  );

  // RED pin: a temp tree with no .git / D2.slnx must fail closed (not silently
  // resolve to a wrong parent via fixed-depth arithmetic).
  const emptyTree = mkdtempSync(join(tmpdir(), "no-repo-sentinel-"));

  try {
    assert.throws(
      () => findRepoRoot(emptyTree),
      /repo-root sentinel/,
      "findRepoRoot must throw when no sentinel exists above startDir",
    );
  } finally {
    rmSync(emptyTree, { recursive: true, force: true, maxRetries: 5 });
  }
});

function runScript(args, { expectFail = false } = {}) {
  try {
    const stdout = execFileSync(BASH, [SCRIPT_ABS, ...args], {
      encoding: "utf-8",
      cwd: REPO_ROOT,
    });

    if (expectFail) {
      assert.fail(`expected non-zero exit for args ${JSON.stringify(args)}`);
    }

    return { status: 0, stdout };
  } catch (err) {
    if (!expectFail) {
      throw err;
    }

    return {
      status: err.status ?? 1,
      stdout: err.stdout?.toString() ?? "",
      stderr: err.stderr?.toString() ?? "",
    };
  }
}

function withTempLog(content, fn) {
  const dir = mkdtempSync(join(tmpdir(), "inspectcode-count-"));

  try {
    const logPath = join(dir, "inspectcode.log");
    writeFileSync(logPath, content, "utf-8");

    return fn(logPath);
  } finally {
    rmSync(dir, { recursive: true, force: true, maxRetries: 5 });
  }
}

test("zero args exits 2 and prints usage", () => {
  const result = runScript([], { expectFail: true });
  assert.equal(result.status, 2);
  const combined = `${result.stdout}\n${result.stderr}`;
  assert.match(
    combined,
    /usage:\s*count-inspectcode-findings\.sh/,
    "zero-args fail path must print usage text",
  );
});

test("missing log path exits 1 and reports log missing", () => {
  const missing = join(
    tmpdir(),
    `inspectcode-missing-${Date.now()}-${Math.random().toString(16).slice(2)}.log`,
  );
  assert.equal(
    existsSync(missing),
    false,
    "path must not exist before RED run",
  );

  const result = runScript([missing], { expectFail: true });
  assert.equal(result.status, 1);
  const combined = `${result.stdout}\n${result.stderr}`;
  assert.match(
    combined,
    /log missing/,
    "missing-log fail path must report log missing",
  );
  assert.match(
    combined,
    /count-inspectcode-findings/,
    "missing-log message must name the script",
  );
});

test("count is 0 for an empty log", () => {
  withTempLog("", (log) => {
    const { stdout } = runScript([log]);
    assert.equal(stdout.trim(), "0");
  });
});

test("count is 0 when only non-indented header lines are present", () => {
  const headerOnly =
    "JetBrains InspectCode 2024.1\n" +
    "Solution: D2.slnx\n" +
    "Inspection report\n";

  withTempLog(headerOnly, (log) => {
    const { stdout } = runScript([log]);
    assert.equal(stdout.trim(), "0");
  });
});

test("counts indented finding-lines only", () => {
  const mixed =
    "JetBrains InspectCode\n" +
    "  Server.Foo.cs:12 Warning Possible null\n" +
    "Solution done\n" +
    "\tServer.Bar.cs:3 Warning Captured disposable\n";

  withTempLog(mixed, (log) => {
    const { stdout } = runScript([log]);
    assert.equal(stdout.trim(), "2");
  });
});

test("--fail exits 1 and dumps the log when finding-lines > 0", () => {
  const body = "Header\n  Finding line\n";

  withTempLog(body, (log) => {
    const result = runScript(["--fail", log], { expectFail: true });
    assert.notEqual(result.status, 0);
    assert.match(result.stdout, /Finding line/);
    // First line of stdout is the count.
    assert.equal(result.stdout.split(/\r?\n/)[0], "1");
  });
});

test("--fail exits 0 when finding-lines == 0", () => {
  withTempLog("Header only\n", (log) => {
    const { status, stdout } = runScript(["--fail", log]);
    assert.equal(status, 0);
    assert.equal(stdout.trim(), "0");
  });
});

test("shared script embeds the canonical indented-line grep", () => {
  const script = readFileSync(SCRIPT_ABS, "utf-8");

  assert.match(
    script,
    /grep -cE ['"]\^\\s\+['"]/,
    "count-inspectcode-findings.sh must own the canonical finding-line grep",
  );
});

test("gates.sh invokes the shared script (no inline inspect parse)", () => {
  const gates = readFileSync(
    join(REPO_ROOT, ".claude/skills/gate-suite/scripts/gates.sh"),
    "utf-8",
  );

  assert.match(
    gates,
    /count-inspectcode-findings\.sh/,
    "gates.sh must call tools/scripts/count-inspectcode-findings.sh",
  );
  assert.doesNotMatch(
    gates,
    /grep -cE ['"]\^\\s\+['"]/,
    "gates.sh must not re-inline the inspectcode finding-line grep",
  );
});

test("test.yml does not run inspectcode as a CI job", () => {
  const yml = readFileSync(
    join(REPO_ROOT, ".github/workflows/test.yml"),
    "utf-8",
  );

  // Local-only by design — no CI job / no shared-script wiring in test.yml.
  assert.doesNotMatch(
    yml,
    /^\s*inspectcode:\s*$/m,
    "test.yml must not define an inspectcode job (local gates.sh only)",
  );
  assert.doesNotMatch(
    yml,
    /count-inspectcode-findings\.sh/,
    "test.yml must not invoke count-inspectcode-findings.sh",
  );
});

test("COMMANDS.md cites the shared inspectcode count script", () => {
  const commands = readFileSync(join(REPO_ROOT, "docs/COMMANDS.md"), "utf-8");

  assert.match(
    commands,
    /count-inspectcode-findings\.sh/,
    "docs/COMMANDS.md must cite the shared script as the parse twin",
  );
});
