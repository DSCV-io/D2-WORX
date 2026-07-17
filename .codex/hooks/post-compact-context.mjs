// Copyright (c) DCSV. All rights reserved.

import { readFileSync, readdirSync, statSync } from "node:fs";
import path from "node:path";

function findRepository(startPath) {
  let current = path.resolve(startPath || process.cwd());

  try {
    if (statSync(current).isFile()) current = path.dirname(current);
  } catch {
    // A missing child path can still have a repository ancestor.
  }

  while (true) {
    const dotGit = path.join(current, ".git");

    try {
      const metadata = statSync(dotGit);
      if (metadata.isDirectory()) return { root: current, gitDir: dotGit };

      if (metadata.isFile()) {
        const match = /^gitdir:\s*(.+)$/im.exec(readFileSync(dotGit, "utf8"));
        if (match)
          return {
            root: current,
            gitDir: path.resolve(current, match[1].trim()),
          };
      }
    } catch {
      // Keep walking toward the filesystem root.
    }

    const parent = path.dirname(current);
    if (parent === current) return null;
    current = parent;
  }
}

function readText(filePath) {
  try {
    return readFileSync(filePath, "utf8").trim();
  } catch {
    return "";
  }
}

function describeHead(gitDir) {
  const head = readText(path.join(gitDir, "HEAD"));
  if (!head) return "unknown";
  if (!head.startsWith("ref:")) return `detached at ${head.slice(0, 12)}`;

  const reference = head.slice("ref:".length).trim();
  return reference.startsWith("refs/heads/")
    ? reference.slice("refs/heads/".length)
    : reference;
}

function recentHeadEntries(gitDir, limit) {
  const log = readText(path.join(gitDir, "logs", "HEAD"));
  if (!log) return [];

  return log
    .split(/\r?\n/)
    .filter(Boolean)
    .slice(-limit)
    .map((line) => {
      const tab = line.indexOf("\t");
      const metadata = tab >= 0 ? line.slice(0, tab) : line;
      const message = tab >= 0 ? line.slice(tab + 1).trim() : "HEAD updated";
      const newSha = metadata.split(/\s+/)[1] ?? "unknown";
      return `${newSha.slice(0, 7)} ${message}`;
    });
}

const repository = findRepository(process.cwd());
const projectRoot = repository?.root ?? process.cwd();

console.log("== D2-WORX post-compact re-orientation ==");
console.log(
  `Branch: ${repository ? describeHead(repository.gitDir) : "unknown"}`,
);
console.log("Recent HEAD entries:");
for (const line of repository ? recentHeadEntries(repository.gitDir, 3) : [])
  console.log(`  ${line}`);
console.log(
  "Canonical instructions: AGENTS.md; workflow: docs/dev/process.md; predicates: docs/dev/rules.md",
);

const journals = [];
function scan(dir, depth) {
  if (depth < 0) return;
  let entries = [];
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) scan(full, depth - 1);
    else if (entry.name === "journal.md")
      journals.push({ full, mtime: statSync(full).mtimeMs });
  }
}
scan(path.join(projectRoot, "docs", "wip"), 2);
journals.sort((a, b) => b.mtime - a.mtime);
if (journals[0])
  console.log(
    `Active deliverable state: ${path.relative(projectRoot, journals[0].full)}`,
  );
else
  console.log(
    "Active deliverable state: inspect docs/wip/ when the task requires it",
  );
console.log(
  "Discovery accelerator: codebase-memory-mcp; resolve project by canonical Git root per docs/dev/codebase-memory.md; disk remains source of truth",
);
