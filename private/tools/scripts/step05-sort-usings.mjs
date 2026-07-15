// Sort file-level using directives (SA1209 + SA1210). Does not touch `using var`.
import { readdirSync, readFileSync, writeFileSync } from "node:fs";
import { join } from "node:path";

const SKIP = new Set([
  "node_modules",
  "bin",
  "obj",
  "dist",
  "coverage",
  ".git",
  "Generated",
]);

function walk(d, out = []) {
  let ents;
  try {
    ents = readdirSync(d, { withFileTypes: true });
  } catch {
    return out;
  }
  for (const e of ents) {
    if (SKIP.has(e.name)) continue;
    const f = join(d, e.name);
    if (e.isDirectory()) walk(f, out);
    else if (e.name.endsWith(".cs")) out.push(f);
  }
  return out;
}

function rank(line) {
  const body = line
    .trim()
    .replace(/^global\s+/, "")
    .replace(/^using\s+/, "")
    .replace(/;$/, "");
  const isGlobal = line.trim().startsWith("global using");
  const isAlias = /\s=\s/.test(body);
  const isStatic = body.startsWith("static ");
  const name = body
    .replace(/^static\s+/, "")
    .split("=")[0]
    .trim();
  const isSystem =
    name.startsWith("System") || name.startsWith("global::System");
  // StyleCop: System ns, other ns, static, aliases. global usings ordered with regular.
  const group = isAlias ? 3 : isStatic ? 2 : isSystem ? 0 : 1;
  return { group, name, isGlobal, line };
}

function sortFile(text) {
  const nl = text.includes("\r\n") ? "\r\n" : "\n";
  const lines = text.split(/\r?\n/);
  let start = -1;
  let end = -1;

  for (let i = 0; i < lines.length; i++) {
    const t = lines[i].trim();
    // file-level only: using X; or global using X; — never "using var"
    const isUsing =
      (t.startsWith("using ") || t.startsWith("global using ")) &&
      t.endsWith(";") &&
      !t.startsWith("using var ") &&
      !t.startsWith("await using ");

    if (isUsing) {
      if (start < 0) start = i;
      end = i;
    } else if (start >= 0) {
      // allow blank lines inside using block? StyleCop prefers contiguous — stop
      if (t === "") {
        // peek ahead: if more usings, include blank? safer stop at blank
        let j = i + 1;
        while (j < lines.length && lines[j].trim() === "") j++;
        const next = j < lines.length ? lines[j].trim() : "";
        if (
          (next.startsWith("using ") || next.startsWith("global using ")) &&
          next.endsWith(";") &&
          !next.startsWith("using var ")
        ) {
          continue;
        }
      }
      break;
    }
  }

  if (start < 0) return text;

  const block = lines.slice(start, end + 1).filter((l) => l.trim() !== "");
  const sorted = block
    .map(rank)
    .sort((a, b) => a.group - b.group || a.name.localeCompare(b.name))
    .map((x) => x.line);

  if (
    sorted.join("\n") === block.join("\n") &&
    end - start + 1 === block.length
  ) {
    return text;
  }

  return [...lines.slice(0, start), ...sorted, ...lines.slice(end + 1)].join(
    nl,
  );
}

const roots = process.argv.slice(2);
let n = 0;
for (const root of roots) {
  for (const f of walk(root)) {
    const o = readFileSync(f, "utf8");
    const t = sortFile(o);
    if (t !== o) {
      writeFileSync(f, t);
      n++;
    }
  }
}
console.log("sorted files", n);
