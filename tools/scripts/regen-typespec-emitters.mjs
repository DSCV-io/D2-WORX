// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * regen-typespec-emitters.mjs
 *
 * Scatter script: compiles contracts/typespec/ and copies each emitted file
 * from dist/generated/ to its committed home.
 *
 * Usage (from the repo root):
 *   node tools/scripts/regen-typespec-emitters.mjs
 *
 * Or via the package alias:
 *   pnpm --filter @d2/typespec-emitters regen
 *
 * The script:
 *   1. Creates temporary NTFS junctions so the TypeSpec compiler can resolve
 *      @d2/* and @typespec/* packages from contracts/typespec/ (which has no
 *      node_modules of its own). Junctions do not require admin on Windows.
 *   2. Writes a temporary main.tsp that imports the same .tsp files listed in
 *      tspconfig.yaml's imports array.
 *   3. Runs `tsp compile` via the emitter package's own node_modules copy.
 *   4. Cleans up junctions and temp files unconditionally (via try/finally).
 *   5. Copies each file from dist/generated/ to its committed home (allowlist —
 *      not copy-all; files without committed homes are intentionally skipped).
 *   6. Prints source → dest for every copy and a final summary.
 *   7. Fails loudly if any expected allowlist entry is missing from dist/generated/.
 *
 * Idempotent: re-running after a clean compile overwrites the committed files
 * with identical content (a no-op from git's perspective).
 */

import { spawnSync } from "node:child_process";
import {
  copyFileSync,
  existsSync,
  mkdirSync,
  rmSync,
  symlinkSync,
  unlinkSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

// ---------------------------------------------------------------------------
// Repo-root resolution (this script lives at tools/scripts/ — two levels up)
// ---------------------------------------------------------------------------

const _SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(_SCRIPT_DIR, "../..");

// ---------------------------------------------------------------------------
// Key directory paths
// ---------------------------------------------------------------------------

const CONTRACTS_DIR = join(REPO_ROOT, "contracts", "typespec");
const EMITTERS_DIR = join(
  REPO_ROOT,
  "server",
  "shared",
  "typescript",
  "typespec-emitters",
);
const EMITTERS_NM = join(EMITTERS_DIR, "node_modules");
const DIST_GENERATED = join(EMITTERS_DIR, "dist", "generated");

// The TypeSpec compiler entry point lives inside the emitter package's own
// node_modules — resolved relative to the emitter package so we use exactly
// the same compiler version the tests and build use.
const TSP_JS = join(EMITTERS_NM, "@typespec", "compiler", "cmd", "tsp.js");

// tspconfig.yaml path (passed to tsp compile via --config).
const TSPCONFIG = join(CONTRACTS_DIR, "tspconfig.yaml");

// Temporary junction paths created before compile and removed after.
const JUNCTION_CONTRACTS_NM = join(CONTRACTS_DIR, "node_modules");
const JUNCTION_SELF_REF = join(EMITTERS_NM, "@d2", "typespec-emitters");

// Temporary main.tsp path — required because `tsp compile <dir>` looks for
// main.tsp. tspconfig.yaml uses `imports:` but a stub main.tsp is still
// needed so the compiler accepts the directory form.
const TEMP_MAIN_TSP = join(CONTRACTS_DIR, "main.tsp");

// ---------------------------------------------------------------------------
// Committed-home mapping: dist/generated/<filename> → repo-root-relative dest
//
// ONLY files where `tsp compile` output is byte-identical to the in-process
// test-host output are listed. This constraint exists because the main
// tsp compile run processes all fixtures together with the real tspconfig.yaml
// (csharp-app-namespace-base set), which routes ALL @d2InProcess / @d2GrpcMethod
// fixture ops into the Clients namespace. In contrast, the byte-gate test suites
// call emitter functions directly with explicit fixture namespaces
// (D2.Edge.Tests.TypeSpecDto.Generated, etc.), producing different C# content
// for the same logical fixtures.
//
// Excluded categories (validated via systematic content comparison):
//   - All C# DTOs for fixture ops (sign, temporal, enum, placeOrder, deepNest, …)
//   - gRPC service, transport-mapper, and client C# files for fixture ops
//   - Route registration C# files (namespace mismatch; registered in app ns not fixture ns)
//   - IKeyCustodianApi.g.cs / KeyCustodianApi.g.cs (include sign + signDerived methods
//     from full compile; committed version is getJwks-only)
//   - .proto files with non-keycustodian package names (fixture protos use per-fixture
//     package names; tsp compile uses d2.keycustodian.v2alpha for all)
//   - predicate-fixtures-grpc-client.g.ts (combined module differs from test-host output)
//
// Files in those categories are governed exclusively by the byte-gate test suites
// (byte-parity.test.ts, proto-grpc-byte-parity.test.ts, etc.) — run `pnpm test`
// to verify them. Update committed fixtures for those files by rerunning the
// relevant test suites and committing the updated outputs when the emitter changes.
// ---------------------------------------------------------------------------

/** @type {ReadonlyArray<{ from: string; to: string }>} */
const COPY_MANIFEST = [
  // ---- GetJwks DTOs (Clients namespace — matches tsp compile routing) ----
  {
    from: "GetJwksInput.g.cs",
    to: "server/services/edge/key-custodian/clients/GetJwksInput.g.cs",
  },
  {
    from: "GetJwksOutput.g.cs",
    to: "server/services/edge/key-custodian/clients/GetJwksOutput.g.cs",
  },
  // NOTE: IKeyCustodianApi.g.cs is EXCLUDED — tsp compile includes sign + signDerived
  // methods (fixture ops also have @d2InProcess); the committed version is getJwks-only.
  // Update it via the facade-emitter.test.ts test-host.

  // ---- GetJwks DI extension (app/Application/) ----
  {
    from: "KeyCustodianClientsGenerated.g.cs",
    to: "server/services/edge/key-custodian/app/Application/KeyCustodianClientsGenerated.g.cs",
  },
  // NOTE: KeyCustodianApi.g.cs is EXCLUDED — tsp compile includes sign + signDerived
  // using lines. The committed version is getJwks-only.

  // ---- GetJwks handler interface (per-op CQRS folder) ----
  {
    from: "IGetJwksHandler.g.cs",
    to: "server/services/edge/key-custodian/app/Application/Handlers/Queries/GetJwks/IGetJwksHandler.g.cs",
  },

  // ---- TypeScript DTOs — no namespace sensitivity, all match ----
  {
    from: "enums-dto.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/enums-dto.g.ts",
  },
  {
    from: "key-custodian-grpc-client.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/key-custodian-grpc-client.g.ts",
  },
  {
    from: "key-custodian-rest-client.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/key-custodian-rest-client.g.ts",
  },
  {
    from: "temporal-dto.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/temporal-dto.g.ts",
  },

  // ---- Sign .proto fixture (namespace matches: keycustodian package) ----
  {
    from: "key_custodian_signer_sign.g.proto",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpc/Protos/key_custodian_signer_sign.g.proto",
  },

  // ---- Enum gRPC TypeScript client (no namespace sensitivity) ----
  {
    from: "enum-fixtures-grpc-client.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Generated/enum-fixtures-grpc-client.g.ts",
  },
  // NOTE: sign-with-kind-dto.g.ts is EXCLUDED — the committed banner uses
  // "<typespec op: signWithKind>" (the fallback form); tsp compile now emits
  // "contracts/typespec/fixtures/enum-shaped.tsp". Update by rerunning
  // byte-parity.test.ts with the updated SIGN_WITH_KIND_DTO_SRC constant.
  // NOTE: enum_fixtures_signer_sign_with_kind.g.proto is EXCLUDED — tsp compile
  // uses package d2.keycustodian.v2alpha; committed fixture uses d2.enumfixtures.v1.

  // ---- Resilience predicate TypeScript files (no namespace sensitivity) ----
  {
    from: "place-order-dto.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-dto.g.ts",
  },
  {
    from: "place-order-resilience-predicates.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-resilience-predicates.g.ts",
  },
  {
    from: "place-order-v2-dto.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-v2-dto.g.ts",
  },
  {
    from: "place-order-v2-resilience-predicates.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-v2-resilience-predicates.g.ts",
  },
  {
    from: "deep-nest-dto.g.ts",
    to: "server/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/deep-nest-dto.g.ts",
  },
  // NOTE: predicate-fixtures-grpc-client.g.ts is EXCLUDED — tsp compile produces
  // a combined module (PlaceOrder + PlaceOrderV2 + DeepNest); the committed
  // fixture was produced by the ts-client-byte-parity test-host with PlaceOrder-only.

  // NOTE: All predicate handler interface C# files (IPlaceOrderHandler.g.cs,
  // IPlaceOrderV2Handler.g.cs, IDeepNestHandler.g.cs), predicate DTO C# files
  // (PlaceOrderInput, PlaceOrderOutput, …), predicate emitter C# files
  // (PlaceOrderResiliencePredicates, …), gRPC client C# files
  // (IPredicateFixturesGrpcClient, PredicateFixturesGrpcClient, PlaceOrderClientKeys,
  // PredicateFixturesGrpcClientsGenerated, PlaceOrderV2ClientKeys, DeepNestClientKeys,
  // D2GeneratedBusinessRetrySignal), and proto files
  // (predicate_fixtures_orders_place_order.g.proto, predicate_fixtures_gizmos_deep_deep_nest.g.proto)
  // are EXCLUDED — namespace mismatch (tsp compile emits D2.Edge.KeyCustodian.* namespaces;
  // committed fixtures use D2.Edge.Tests.TypeSpecGrpcPredicate.Generated).
  // Handler interfaces and DTOs are governed by predicate-byte-parity.test.ts (which calls
  // emitHandlerInterface / emitCsharpDtos directly with the fixture namespace) and
  // nested-model-grpc-byte-parity.test.ts. Never scatter these from $onEmit output.

  // NOTE: OpenAPI (TypeSpecOpenApi/Generated/), SSE (TypeSpecSse/Generated/),
  // route registrations (TypeSpecRoute/Generated/), and all other gRPC service /
  // transport-mapper / C# DTO files are EXCLUDED for similar namespace-mismatch
  // reasons. Update them via the respective integration test suites.
];

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Remove a path if it exists, supporting both junction/symlink and real dirs.
 * @param {string} p
 */
function removeSafe(p) {
  if (!existsSync(p)) return;
  try {
    // Attempt symlink/junction removal first (unlinkSync on a junction).
    unlinkSync(p);
  } catch {
    // Fall back to recursive removal for real directories.
    rmSync(p, { recursive: true, force: true });
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

console.log("regen-typespec-emitters: compiling contracts/typespec/ …\n");

// Validate prerequisites.
if (!existsSync(TSP_JS)) {
  console.error(`ERROR: TypeSpec compiler not found at:\n  ${TSP_JS}`);
  console.error("Run pnpm install in the emitter package first.");
  process.exit(1);
}

if (!existsSync(TSPCONFIG)) {
  console.error(`ERROR: tspconfig.yaml not found at:\n  ${TSPCONFIG}`);
  process.exit(1);
}

// Create temp artifacts — removed in the finally block.
let junctionContractsNmCreated = false;
let junctionSelfRefCreated = false;
let tempMainTspCreated = false;

try {
  // ---- 1. Junction: contracts/typespec/node_modules → emitters/node_modules ----
  //
  // TypeSpec's module resolver walks up from the imported .tsp file to find
  // node_modules. contracts/typespec/ has none, so we bridge it to the emitter
  // package's node_modules. NTFS junctions do not require admin on Windows.
  removeSafe(JUNCTION_CONTRACTS_NM);
  symlinkSync(EMITTERS_NM, JUNCTION_CONTRACTS_NM, "junction");
  junctionContractsNmCreated = true;

  // ---- 2. Junction: emitters/node_modules/@d2/typespec-emitters → emitters/ ----
  //
  // The emitter itself is a workspace package — it's not installed in its own
  // node_modules/@d2/typespec-emitters/. tsp compile resolves the emitter
  // declared in tspconfig.yaml's `emit:` array via normal module resolution, so
  // a self-referencing junction is required.
  const d2NmDir = join(EMITTERS_NM, "@d2");

  if (!existsSync(d2NmDir)) mkdirSync(d2NmDir, { recursive: true });

  removeSafe(JUNCTION_SELF_REF);
  symlinkSync(EMITTERS_DIR, JUNCTION_SELF_REF, "junction");
  junctionSelfRefCreated = true;

  // ---- 3. Temporary main.tsp ----
  //
  // `tsp compile <dir>` requires a main.tsp in the target directory. The actual
  // imports are declared in tspconfig.yaml; main.tsp is a stub so the directory
  // form of the compile command is accepted.
  writeFileSync(
    TEMP_MAIN_TSP,
    [
      'import "./key-custodian/key-custodian.tsp";',
      'import "./fixtures/sign-shaped.tsp";',
      'import "./fixtures/temporal-shaped.tsp";',
      'import "./fixtures/enum-shaped.tsp";',
      'import "./fixtures/resilience-predicate-shaped.tsp";',
      "",
    ].join("\n"),
    "utf8",
  );
  tempMainTspCreated = true;

  // ---- 4. Run tsp compile ----
  const tspResult = spawnSync(
    process.execPath,
    [TSP_JS, "compile", CONTRACTS_DIR, "--config", TSPCONFIG],
    {
      cwd: CONTRACTS_DIR,
      encoding: "utf8",
      stdio: "pipe",
    },
  );

  if (tspResult.stdout) process.stdout.write(tspResult.stdout);
  if (tspResult.stderr) process.stderr.write(tspResult.stderr);

  if (tspResult.status !== 0) {
    console.error(
      `\nERROR: tsp compile failed with exit code ${tspResult.status}.`,
    );
    process.exit(tspResult.status ?? 1);
  }

  console.log("Compilation succeeded.\n");
} finally {
  // ---- 5. Unconditional cleanup ----
  if (tempMainTspCreated) {
    try {
      unlinkSync(TEMP_MAIN_TSP);
    } catch {
      // Non-fatal; the file lives in contracts/ which is gitignored-free, so
      // leaving a stale main.tsp would be noticed immediately. Log and continue.
      console.warn(`WARN: could not remove temp ${TEMP_MAIN_TSP}`);
    }
  }

  if (junctionSelfRefCreated) {
    try {
      removeSafe(JUNCTION_SELF_REF);
    } catch {
      console.warn(`WARN: could not remove junction ${JUNCTION_SELF_REF}`);
    }
  }

  if (junctionContractsNmCreated) {
    try {
      removeSafe(JUNCTION_CONTRACTS_NM);
    } catch {
      console.warn(`WARN: could not remove junction ${JUNCTION_CONTRACTS_NM}`);
    }
  }
}

// ---- 6. Copy allowlist entries to committed homes ----

let copied = 0;
const missing = [];

for (const { from, to } of COPY_MANIFEST) {
  const src = join(DIST_GENERATED, from);
  const dest = join(REPO_ROOT, to);

  if (!existsSync(src)) {
    missing.push(src);
    continue;
  }

  // Ensure destination directory exists (in case a new committed home was added
  // to the manifest before the directory was created).
  mkdirSync(dirname(dest), { recursive: true });

  copyFileSync(src, dest);
  console.log(`  ${from}`);
  console.log(`    → ${to}`);
  copied++;
}

if (missing.length > 0) {
  console.error("\nERROR: expected output files missing from dist/generated/:");

  for (const m of missing) console.error(`  ${m}`);

  console.error(
    "\nThe compile succeeded but some expected outputs were not produced.",
  );
  console.error(
    "Check whether the corresponding .tsp definitions were removed or renamed.",
  );
  process.exit(1);
}

console.log(
  `\n✓ Regenerated ${copied} committed fixture${copied === 1 ? "" : "s"}.`,
);
console.log(
  "  Run `pnpm --filter @d2/typespec-emitters test` to confirm byte-gate tests pass.",
);
