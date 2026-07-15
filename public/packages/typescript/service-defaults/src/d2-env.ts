// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { existsSync, readFileSync } from "node:fs";
import { dirname, resolve } from "node:path";

import { falsey } from "@dcsv-io/d2-utilities";

/**
 * Options for {@link D2Env.load}. Used in tests to inject explicit
 * file lookup paths without relying on cwd discovery.
 */
export interface D2EnvLoadOptions {
  /** Starting directory for upward-discovery (defaults to `process.cwd()`). */
  readonly startDir?: string;
  /** Override the env-file basenames to look for. */
  readonly fileNames?: readonly string[];
  /** Reads come from this dictionary instead of `process.env` if supplied. */
  readonly env?: Record<string, string | undefined>;
}

const DEFAULT_FILES = [".env.secrets", ".env.local", ".env"];

/**
 * Layered env loader. Mirrors .NET `D2Env.Load()` — discovers the
 * nearest `.env` / `.env.local` / `.env.secrets` upward from `startDir`,
 * loads each into a layered map, then composes with `process.env` (env
 * vars win). Designed to be called once at process boot.
 */
export const D2Env = {
  load(opts: D2EnvLoadOptions = {}): Record<string, string> {
    const fileNames = opts.fileNames ?? DEFAULT_FILES;
    const baseEnv = opts.env ?? process.env;
    const merged: Record<string, string> = {};

    // Search upward from startDir for each file name; load in REVERSE
    // priority order so higher-priority files (earlier in fileNames)
    // overwrite lower-priority entries on conflict.
    const startDir = opts.startDir ?? process.cwd();
    for (let i = fileNames.length - 1; i >= 0; i--) {
      const fileName = fileNames[i]!;
      const path = D2Env.discoverFile(startDir, fileName);
      if (path === undefined) continue;
      const fileEntries = D2Env.parseEnvFile(readFileSync(path, "utf8"));
      for (const [k, v] of Object.entries(fileEntries)) merged[k] = v;
    }

    // Real env vars take precedence over file values.
    for (const [k, v] of Object.entries(baseEnv)) {
      if (v !== undefined) merged[k] = v;
    }

    return merged;
  },

  discoverFile(startDir: string, fileName: string): string | undefined {
    let cur = resolve(startDir);
    while (true) {
      const candidate = resolve(cur, fileName);
      if (existsSync(candidate)) return candidate;
      const parent = dirname(cur);
      if (parent === cur) return undefined;
      cur = parent;
    }
  },

  parseEnvFile(content: string): Record<string, string> {
    const out: Record<string, string> = {};
    for (const rawLine of content.split(/\r?\n/)) {
      const line = rawLine.trim();
      if (falsey(line) || line.startsWith("#")) continue;
      const eq = line.indexOf("=");
      if (eq <= 0) continue;
      const key = line.slice(0, eq).trim();
      const rawVal = line.slice(eq + 1).trim();
      const val = stripQuotes(rawVal);
      out[key] = val;
    }
    return out;
  },
};

function stripQuotes(s: string): string {
  if (s.length >= 2) {
    const first = s[0];
    const last = s[s.length - 1];
    if ((first === '"' && last === '"') || (first === "'" && last === "'"))
      return s.slice(1, -1);
  }
  return s;
}
