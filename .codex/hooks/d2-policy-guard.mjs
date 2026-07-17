// Copyright (c) DCSV. All rights reserved.

import { existsSync, readFileSync, statSync } from "node:fs";
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

let input = "";
for await (const chunk of process.stdin) input += chunk;

let event = {};
try {
  event = input ? JSON.parse(input) : {};
} catch {
  process.exit(0);
}

const toolName = String(event.tool_name ?? event.toolName ?? "");
const toolInput = event.tool_input ?? event.toolInput ?? {};
const isObjectInput =
  toolInput !== null &&
  typeof toolInput === "object" &&
  !Array.isArray(toolInput);

function textFields(value, names) {
  if (typeof value === "string") return [value];
  if (!isObjectInput) return [];
  return names
    .map((name) => value[name])
    .filter((item) => typeof item === "string");
}

const commandTexts = textFields(toolInput, ["command", "cmd"]);
const command = commandTexts.join("\n");
const normalized = command
  .replace(/[\r\n\t]+/g, " ")
  .replace(/\s+/g, " ")
  .trim();

const invocationDirectory = event.cwd ?? process.cwd();
const projectRoot =
  findRepository(invocationDirectory)?.root ??
  path.resolve(invocationDirectory);
const marker = path.join(projectRoot, ".claude", ".commit-authorized");

function block(reason) {
  process.stderr.write(`BLOCKED by D2 policy guard: ${reason}\n`);
  process.exit(2);
}

function shellLex(value) {
  const tokens = [];
  let ambiguousEscape = false;
  let word = "";
  let quote = "";

  function pushWord() {
    if (word) tokens.push({ type: "word", value: word });
    word = "";
  }

  for (let index = 0; index < value.length; index += 1) {
    const character = value[index];
    const nextCharacter = value[index + 1] ?? "";

    if (
      character.charCodeAt(0) === 96 ||
      (character === "\\" &&
        nextCharacter &&
        /[\s"';|&()<>]/.test(nextCharacter))
    ) {
      ambiguousEscape = true;
    }

    if (quote) {
      if (character === quote) quote = "";
      else word += character;
      continue;
    }

    if (character === '"' || character === "'") {
      quote = character;
    } else if (/\s/.test(character)) {
      pushWord();
    } else if (/[;|&()<>]/.test(character)) {
      pushWord();
      const doubled =
        value[index + 1] === character && /[|&<>]/.test(character);
      tokens.push({
        type: "control",
        value: doubled ? character + character : character,
      });
      if (doubled) index += 1;
    } else {
      word += character;
    }
  }

  pushWord();
  return { ambiguousEscape, tokens };
}

function protectedPath(value) {
  const candidate = String(value).replace(/\\/g, "/");
  const boundary = "[^A-Za-z0-9_.-]";
  return (
    new RegExp(`(?:^|${boundary})\\.env\\.secrets(?=$|${boundary})`, "i").test(
      candidate,
    ) ||
    new RegExp(
      `(?:^|${boundary})(?:secrets|\\.aws|\\.ssh)(?=$|${boundary})`,
      "i",
    ).test(candidate) ||
    new RegExp(
      `(?:^|${boundary})(?:\\.npmrc|[A-Za-z0-9_.-]+\\.(?:pem|key))(?=$|${boundary})`,
      "i",
    ).test(candidate)
  );
}

const isPatch = toolName.toLowerCase() === "apply_patch";
const patchTexts = isPatch
  ? textFields(toolInput, ["patch", "input", "text", "command", "cmd"])
  : [];

const commandLex = shellLex(command);
const commandTokens = commandLex.tokens;

if (!isPatch && commandLex.ambiguousEscape) {
  block(
    "the command contains ambiguous shell escaping and cannot be safely classified",
  );
}

if (
  !isPatch &&
  commandTokens.some(
    (token) => token.type === "word" && protectedPath(token.value),
  )
) {
  block("the command references a deny-ruled secret or key-material path");
}

const candidatePaths = isObjectInput
  ? [toolInput.path, toolInput.file_path, toolInput.filename].filter(Boolean)
  : [];
if (isPatch) {
  for (const patchText of patchTexts) {
    for (const match of patchText.matchAll(
      /^\*\*\* (?:Add|Update|Delete) File: (.+)$/gm,
    )) {
      candidatePaths.push(match[1]);
    }
  }
}
if (candidatePaths.some(protectedPath)) {
  block("the file operation targets a deny-ruled secret or key-material path");
}

if (!normalized) process.exit(0);

let matched = "";
const gitOptionsWithValues = new Set([
  "-c",
  "--config-env",
  "--git-dir",
  "--namespace",
  "--super-prefix",
  "--work-tree",
]);

function gitInvocations(tokens) {
  const invocations = [];

  for (
    let executableIndex = 0;
    executableIndex < tokens.length;
    executableIndex += 1
  ) {
    const executable = tokens[executableIndex];
    if (executable.type !== "word") continue;

    const executableName =
      executable.value.replace(/\\/g, "/").split("/").at(-1) ?? "";
    if (!/^git(?:\.exe)?$/i.test(executableName)) continue;

    let index = executableIndex + 1;

    while (
      tokens[index]?.type === "word" &&
      tokens[index].value.startsWith("-")
    ) {
      const option = tokens[index].value;
      const inlineValue =
        /^-[cC].+/.test(option) ||
        /^--(?:config-env|git-dir|namespace|super-prefix|work-tree)=/i.test(
          option,
        );
      const consumesNext =
        !inlineValue && gitOptionsWithValues.has(option.toLowerCase());
      index += consumesNext && tokens[index + 1]?.type === "word" ? 2 : 1;
    }

    if (tokens[index]?.type !== "word") continue;
    const argumentsList = [];
    for (
      let argumentIndex = index + 1;
      tokens[argumentIndex]?.type === "word";
      argumentIndex += 1
    ) {
      argumentsList.push(tokens[argumentIndex].value);
    }

    invocations.push({
      subcommand: tokens[index].value.toLowerCase(),
      arguments: argumentsList,
    });
  }

  return invocations;
}

function branchDeletes(argumentsList) {
  for (const argument of argumentsList) {
    if (argument === "--") break;
    if (
      argument === "--delete" ||
      (/^-[^-]+$/.test(argument) && /[dD]/.test(argument.slice(1)))
    ) {
      return true;
    }
  }

  return false;
}

const guardedSubcommands = new Map([
  ["commit", "git commit"],
  ["push", "git push"],
  ["reset", "git reset"],
  ["clean", "git clean"],
  ["restore", "git restore"],
  ["rm", "git rm"],
  ["rebase", "git rebase"],
]);

for (const invocation of gitInvocations(commandTokens)) {
  const firstArgument = invocation.arguments[0] ?? "";

  if (
    invocation.subcommand === "stash" &&
    !new Set(["list", "show"]).has(firstArgument)
  ) {
    matched = "git stash";
  } else if (
    invocation.subcommand === "checkout" &&
    !new Set(["--help", "-h"]).has(firstArgument)
  ) {
    matched = "git checkout";
  } else if (
    invocation.subcommand === "branch" &&
    branchDeletes(invocation.arguments)
  ) {
    matched = "git branch delete";
  } else if (
    invocation.subcommand === "worktree" &&
    firstArgument === "remove"
  ) {
    matched = "git worktree remove";
  } else {
    matched = guardedSubcommands.get(invocation.subcommand) ?? "";
  }

  if (matched) break;
}

if (matched && !existsSync(marker)) {
  block(
    `${matched} requires explicit per-occurrence authorization and the sanctioned one-shot ${path.relative(projectRoot, marker)} marker`,
  );
}
