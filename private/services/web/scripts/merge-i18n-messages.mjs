// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------
// Merge public∪private locale catalogs for Paraglide. Writes to
// private/contracts/messages-merged/ (gitignored) so pathPattern can use the
// same depth as the historical private contracts path (paraglide/inlang
// resolves reliably under ../../contracts/…).

import { existsSync, mkdirSync, readdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const webRoot = join(here, "..");
const repoRoot = join(webRoot, "..", "..", "..");
const publicMsg = join(repoRoot, "public", "contracts", "messages");
const privateMsg = join(repoRoot, "private", "contracts", "messages");
const outDir = join(repoRoot, "private", "contracts", "messages-merged");

mkdirSync(outDir, { recursive: true });

const locales = new Set();
for (const dir of [publicMsg, privateMsg]) {
  if (!existsSync(dir)) continue;
  for (const name of readdirSync(dir)) {
    if (name.endsWith(".json")) locales.add(name);
  }
}

for (const file of [...locales].sort()) {
  const merged = {};
  for (const dir of [publicMsg, privateMsg]) {
    const abs = join(dir, file);
    if (!existsSync(abs)) continue;
    const doc = JSON.parse(readFileSync(abs, "utf8"));
    for (const [k, v] of Object.entries(doc)) {
      if (k === "$schema") continue;
      if (typeof v === "string") merged[k] = v;
    }
  }
  const ordered = {};
  for (const k of Object.keys(merged).sort()) ordered[k] = merged[k];
  writeFileSync(join(outDir, file), `${JSON.stringify(ordered, null, 2)}\n`, "utf8");
}

console.log(
  `[merge-i18n-messages] wrote ${locales.size} locale file(s) → private/contracts/messages-merged/`,
);
