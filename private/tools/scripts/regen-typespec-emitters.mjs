// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * regen-typespec-emitters.mjs
 *
 * Scatter script: compiles each per-module TypeSpec package under
 * contracts/typespec/ and copies each emitted file from dist/generated/ to its
 * committed home.
 *
 * Usage (from the monorepo root):
 *   node private/tools/scripts/regen-typespec-emitters.mjs
 *
 * Or via the package alias:
 *   pnpm --filter @dcsv-io/d2-typespec-emitters regen
 *
 * The script:
 *   1. Creates temporary NTFS junctions so the TypeSpec compiler can resolve
 *      @dcsv-io/d2-* and @typespec/* packages from private/contracts/typespec/
 *      (which has no node_modules of its own). Junctions do not require admin
 *      on Windows. Also junctions public fixtures into the private contracts
 *      tree when product packages co-import shared fixture .tsp files.
 *   2. For EACH module package (KeyCustodian, Audit, …):
 *        a. Writes a temporary main.tsp matching that package's imports
 *        b. Runs `tsp compile --config <module>/tspconfig.yaml`
 *           (emitter-output-dir → public emitters dist/generated; nested
 *           configs use an extra ../ so the resolve target stays identical)
 *        c. COPY_MANIFEST subset for that package only (fail-loud missing)
 *      NEVER "compile all packages then one COPY" — shared dist/generated is
 *      clobbered by each compile; COPY must run between packages.
 *   3. Cleans up junctions and temp files unconditionally (via try/finally).
 *   4. Prints source → dest for every copy and a final summary.
 *
 * Root public/contracts/typespec/tspconfig.yaml is RETIRED (pointer only) —
 * do not use it as the primary compile entry for product modules.
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
// Repo-root resolution (this script lives at private/tools/scripts/ —
// monorepo root = walk-up to pnpm-workspace.yaml / D2.slnx / .git).
// ---------------------------------------------------------------------------

const _SCRIPT_DIR = dirname(fileURLToPath(import.meta.url));

/**
 * @param {string} startDir
 * @returns {string}
 */
function findRepoRoot(startDir) {
  let current = resolve(startDir);

  while (true) {
    if (
      existsSync(join(current, "pnpm-workspace.yaml")) ||
      existsSync(join(current, "D2.slnx")) ||
      existsSync(join(current, ".git"))
    ) {
      return current;
    }

    const parent = dirname(current);

    if (parent === current) {
      throw new Error(
        `repo-root sentinel: no pnpm-workspace.yaml / D2.slnx / .git walking up from ${startDir}`,
      );
    }

    current = parent;
  }
}

const REPO_ROOT = findRepoRoot(_SCRIPT_DIR);

// ---------------------------------------------------------------------------
// Key directory paths (dual-tree: product contracts private, emitters public)
// ---------------------------------------------------------------------------

const CONTRACTS_DIR = join(REPO_ROOT, "private", "contracts", "typespec");
const PUBLIC_FIXTURES_DIR = join(
  REPO_ROOT,
  "public",
  "contracts",
  "typespec",
  "fixtures",
);
const EMITTERS_DIR = join(
  REPO_ROOT,
  "public",
  "packages",
  "typescript",
  "typespec-emitters",
);
const EMITTERS_NM = join(EMITTERS_DIR, "node_modules");
const DIST_GENERATED = join(EMITTERS_DIR, "dist", "generated");

// The TypeSpec compiler entry point lives inside the emitter package's own
// node_modules — resolved relative to the emitter package so we use exactly
// the same compiler version the tests and build use.
const TSP_JS = join(EMITTERS_NM, "@typespec", "compiler", "cmd", "tsp.js");

// Temporary junction paths created before compile and removed after.
const PUBLIC_CONTRACTS_TYPESPEC = join(
  REPO_ROOT,
  "public",
  "contracts",
  "typespec",
);
const JUNCTION_CONTRACTS_NM = join(CONTRACTS_DIR, "node_modules");
const JUNCTION_PUBLIC_CONTRACTS_NM = join(
  PUBLIC_CONTRACTS_TYPESPEC,
  "node_modules",
);
const JUNCTION_SELF_REF = join(EMITTERS_NM, "@dcsv-io", "d2-typespec-emitters");
// Product tspconfigs still import `../fixtures/*.tsp` relative to the private
// contracts tree; shared fixture .tsp files live under public/contracts.
const JUNCTION_PRIVATE_FIXTURES = join(CONTRACTS_DIR, "fixtures");

// ---------------------------------------------------------------------------
// Per-module packages: each owns tspconfig + COPY subset.
// Sequence lock: compileᵢ → COPYᵢ → compileᵢ₊₁ (never multi-compile then one COPY).
// ---------------------------------------------------------------------------

/**
 * @typedef {{ from: string; to: string }} CopyRow
 * @typedef {{
 *   name: string;
 *   configRel: string;
 *   projectDirRel: string;
 *   mainImports: readonly string[];
 *   copy: readonly CopyRow[];
 * }} PackageSpec
 */

// ---------------------------------------------------------------------------
// KeyCustodian package COPY subset (KC live + co-compiled fixture TS rows).
//
// ONLY files where package compile output is byte-identical to the in-process
// test-host output for production homes are listed. Fixture C# (wrong ns) stays
// governed by byte-gate suites — see NOTES at end of this array historically.
// ---------------------------------------------------------------------------

/** @type {ReadonlyArray<CopyRow>} */
const KEY_CUSTODIAN_COPY = [
  // ---- GetJwks DTOs (Client namespace, Jwks concern — @d2Concern("Jwks")) ----
  {
    from: "GetJwksInput.g.cs",
    to: "private/services/edge/key-custodian/client/Jwks/GetJwksInput.g.cs",
  },
  {
    from: "GetJwksOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Jwks/GetJwksOutput.g.cs",
  },

  // ---- OIDC discovery DTOs (Client namespace, OidcConfiguration concern) ----
  {
    from: "GetOidcConfigurationInput.g.cs",
    to: "private/services/edge/key-custodian/client/OidcConfiguration/GetOidcConfigurationInput.g.cs",
  },
  {
    from: "GetOidcConfigurationOutput.g.cs",
    to: "private/services/edge/key-custodian/client/OidcConfiguration/GetOidcConfigurationOutput.g.cs",
  },

  // ---- Sign DTOs (Client namespace, Signing concern) ----
  {
    from: "SignInput.g.cs",
    to: "private/services/edge/key-custodian/client/Signing/SignInput.g.cs",
  },
  {
    from: "SignOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Signing/SignOutput.g.cs",
  },

  // ---- GetKeyring DTOs (Client namespace, Keyring concern; GetKeyringOutput.g.cs
  //      also carries the nested KeyringEntry record with keyBytes redacted
  //      SecretInformation) ----
  {
    from: "GetKeyringInput.g.cs",
    to: "private/services/edge/key-custodian/client/Keyring/GetKeyringInput.g.cs",
  },
  {
    from: "GetKeyringOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Keyring/GetKeyringOutput.g.cs",
  },

  // ---- IssueLeaf DTOs (Client namespace, Issuance concern; all-public issuance
  //      material, no redaction — CSR in, cert out) ----
  {
    from: "IssueLeafInput.g.cs",
    to: "private/services/edge/key-custodian/client/Issuance/IssueLeafInput.g.cs",
  },
  {
    from: "IssueLeafOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Issuance/IssueLeafOutput.g.cs",
  },

  // ---- GetCaCertificate DTOs (Client namespace, CaCertificate concern; empty
  //      input, public root+intermediate chain output) ----
  {
    from: "GetCaCertificateInput.g.cs",
    to: "private/services/edge/key-custodian/client/CaCertificate/GetCaCertificateInput.g.cs",
  },
  {
    from: "GetCaCertificateOutput.g.cs",
    to: "private/services/edge/key-custodian/client/CaCertificate/GetCaCertificateOutput.g.cs",
  },

  // ---- Sealing DTOs (Client namespace, Sealing concern; getOrLazyProvisionSealPublicKey =
  //      public SPKI entries (no redaction), getOrLazyProvisionOwnSealPrivateKey = private PKCS#8
  //      entries with privatePkcs8 redacted SecretInformation; getOrLazyProvisionOwnSealPrivateKey
  //      is parameterless so its Input DTO is the synthesized empty record) ----
  {
    from: "GetOrLazyProvisionSealPublicKeyInput.g.cs",
    to: "private/services/edge/key-custodian/client/Sealing/GetOrLazyProvisionSealPublicKeyInput.g.cs",
  },
  {
    from: "GetOrLazyProvisionSealPublicKeyOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Sealing/GetOrLazyProvisionSealPublicKeyOutput.g.cs",
  },
  {
    from: "GetOrLazyProvisionOwnSealPrivateKeyInput.g.cs",
    to: "private/services/edge/key-custodian/client/Sealing/GetOrLazyProvisionOwnSealPrivateKeyInput.g.cs",
  },
  {
    from: "GetOrLazyProvisionOwnSealPrivateKeyOutput.g.cs",
    to: "private/services/edge/key-custodian/client/Sealing/GetOrLazyProvisionOwnSealPrivateKeyOutput.g.cs",
  },

  // ---- Module façade interface + impl (real KC ops only). The façade lives in
  //      a Facade/ folder → namespace <clients-ns>.Facade (interface) /
  //      <app-ns>.Facade (impl). The facade-emitter.test.ts byte-gate pins them
  //      independently. ----
  {
    from: "IKeyCustodianApi.g.cs",
    to: "private/services/edge/key-custodian/client/Facade/IKeyCustodianApi.g.cs",
  },
  {
    from: "KeyCustodianApi.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Facade/KeyCustodianApi.g.cs",
  },

  // ---- Façade DI extension (app/Application/Facade/). The file + method names
  //      derive from the clients-namespace leaf ("Client"), so the identifier is
  //      the non-plural KeyCustodianClientGenerated.g.cs / AddD2KeyCustodianClient(). ----
  {
    from: "KeyCustodianClientGenerated.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Facade/KeyCustodianClientGenerated.g.cs",
  },

  // ---- KeyCustodian gRPC service impls (production Edge.Api). The global tsp
  //      compile emits these into grpc-service-namespace
  //      DcsvIo.D2.Private.Edge.Api.Grpc.KeyCustodian — matching the committed production home —
  //      delegating to the facade IKeyCustodianApi (<clients-ns>.Facade).
  //      Physical home: api/Grpc/KeyCustodian/ (ADR-0020 transport).
  //      Fixture gRPC services (SignFixture*, enum, predicate) stay excluded
  //      (COPY_MANIFEST allowlist + fixture byte-parity hardcodes test ns). ----
  {
    from: "KeyCustodianSignerService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianSignerService.g.cs",
  },
  {
    from: "KeyCustodianKeyringService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianKeyringService.g.cs",
  },
  {
    from: "KeyCustodianCertificateAuthorityService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianCertificateAuthorityService.g.cs",
  },
  {
    from: "KeyCustodianCaCertificateService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianCaCertificateService.g.cs",
  },
  {
    from: "KeyCustodianSealPublicKeyService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianSealPublicKeyService.g.cs",
  },
  {
    from: "KeyCustodianOwnSealPrivateKeyService.g.cs",
    to: "private/services/edge/api/Grpc/KeyCustodian/KeyCustodianOwnSealPrivateKeyService.g.cs",
  },

  // ---- KeyCustodian transport mappers (production Edge.Api Mappers/KeyCustodian).
  //      Namespace co-located with service-impl (emitter single serviceImplNs);
  //      physical folder = ADR-0020 transport mapper home. ----
  {
    from: "SignTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/SignTransportMappers.g.cs",
  },
  {
    from: "GetKeyringTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/GetKeyringTransportMappers.g.cs",
  },
  {
    from: "IssueLeafTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/IssueLeafTransportMappers.g.cs",
  },
  {
    from: "GetCaCertificateTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/GetCaCertificateTransportMappers.g.cs",
  },
  {
    from: "GetOrLazyProvisionSealPublicKeyTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/GetOrLazyProvisionSealPublicKeyTransportMappers.g.cs",
  },
  {
    from: "GetOrLazyProvisionOwnSealPrivateKeyTransportMappers.g.cs",
    to: "private/services/edge/api/Mappers/KeyCustodian/GetOrLazyProvisionOwnSealPrivateKeyTransportMappers.g.cs",
  },

  // ---- KeyCustodian production .g.proto files (Edge.Api Protos/KeyCustodian).
  //      Compile-once: Edge.Api Grpc.Tools Both owns sign/issue/cacert;
  //      Client Grpc.Tools Both owns keyring + two seal protos (paths retarget
  //      here). Fixture protos (sign_fixture_*, enum, predicate) stay under tests. ----
  {
    from: "key_custodian_signer_sign.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_signer_sign.g.proto",
  },
  {
    from: "key_custodian_certificate_authority_issue_workload_certificate.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_certificate_authority_issue_workload_certificate.g.proto",
  },
  {
    from: "key_custodian_ca_certificate_get_ca_certificate.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_ca_certificate_get_ca_certificate.g.proto",
  },
  {
    from: "key_custodian_keyring_get_keyring.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_keyring_get_keyring.g.proto",
  },
  {
    from: "key_custodian_seal_public_key_get_or_lazy_provision_seal_public_key.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_seal_public_key_get_or_lazy_provision_seal_public_key.g.proto",
  },
  {
    from: "key_custodian_own_seal_private_key_get_or_lazy_provision_own_seal_private_key.g.proto",
    to: "private/services/edge/api/Protos/KeyCustodian/key_custodian_own_seal_private_key_get_or_lazy_provision_own_seal_private_key.g.proto",
  },

  // ---- Handler interfaces (per-op CQRS folder) ----
  {
    from: "IGetJwksHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Queries/GetJwks/IGetJwksHandler.g.cs",
  },
  {
    from: "IGetOidcConfigurationHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Queries/GetOidcConfiguration/IGetOidcConfigurationHandler.g.cs",
  },
  {
    from: "ISignHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Queries/Sign/ISignHandler.g.cs",
  },
  {
    from: "IGetKeyringHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Queries/GetKeyring/IGetKeyringHandler.g.cs",
  },
  {
    from: "IIssueLeafHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Commands/IssueLeaf/IIssueLeafHandler.g.cs",
  },
  {
    from: "IGetCaCertificateHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Queries/GetCaCertificate/IGetCaCertificateHandler.g.cs",
  },
  {
    from: "IGetOrLazyProvisionSealPublicKeyHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Commands/GetOrLazyProvisionSealPublicKey/IGetOrLazyProvisionSealPublicKeyHandler.g.cs",
  },
  {
    from: "IGetOrLazyProvisionOwnSealPrivateKeyHandler.g.cs",
    to: "private/services/edge/key-custodian/app/Application/Handlers/Commands/GetOrLazyProvisionOwnSealPrivateKey/IGetOrLazyProvisionOwnSealPrivateKeyHandler.g.cs",
  },

  // ---- Well-known route registrations (production home Edge.Api;
  //      ns DcsvIo.D2.Private.Edge.Api.Routes.KeyCustodian via csharp-routes-namespace +
  //      process-kind-by-module KeyCustodian=edge-module; delegate to
  //      IKeyCustodianApi, both @d2Harmless GET). Tests ProjectReference
  //      DcsvIo.D2.Private.Edge.Api — do NOT dual-home under tests/Generated (CS0433). ----
  {
    from: "GetJwksRouteRegistration.g.cs",
    to: "private/services/edge/api/Routes/KeyCustodian/GetJwksRouteRegistration.g.cs",
  },
  {
    from: "GetOidcConfigurationRouteRegistration.g.cs",
    to: "private/services/edge/api/Routes/KeyCustodian/GetOidcConfigurationRouteRegistration.g.cs",
  },

  // ---- TypeScript DTOs — no namespace sensitivity, all match ----
  {
    from: "enum-fixture-dto.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/enum-fixture-dto.g.ts",
  },
  {
    from: "sign-fixture-grpc-client.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/sign-fixture-grpc-client.g.ts",
  },
  {
    from: "sign-fixture-rest-client.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/sign-fixture-rest-client.g.ts",
  },
  {
    from: "temporal-fixture-dto.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecDto/Generated/temporal-fixture-dto.g.ts",
  },

  // NOTE: the sign-shaped fixture proto is EXCLUDED — after the wire-identity
  // rename the fixture carries the synthetic per-fixture package d2.signfixtures.v2alpha,
  // which the GLOBAL tspconfig compile (proto-package d2.keycustodian.v2alpha, the
  // REAL KC ops) no longer matches. Like the enum / predicate fixture protos it is
  // now governed exclusively by the byte-gate test suites (proto-grpc-byte-parity.test.ts)
  // — never scattered from $onEmit output.

  // ---- Enum gRPC TypeScript client (no namespace sensitivity; served-by EnumFixtures
  //      is unchanged so the file name is stable) ----
  {
    from: "enum-fixtures-grpc-client.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcEnum/Generated/enum-fixtures-grpc-client.g.ts",
  },
  // NOTE: sign-with-kind-fixture-dto.g.ts is EXCLUDED — the committed banner uses
  // "<typespec op: signWithKindFixture>" (the per-op fallback form); tsp compile now emits
  // "contracts/typespec/fixtures/enum-shaped.tsp". Update by rerunning
  // byte-parity.test.ts with the updated SWK DTO source constant.
  // NOTE: enum_fixtures_signer_sign_with_kind_fixture.g.proto is EXCLUDED — tsp compile
  // uses package d2.keycustodian.v2alpha; committed fixture uses d2.enumfixtures.v1.

  // ---- Resilience predicate TypeScript files (no namespace sensitivity) ----
  {
    from: "place-order-fixture-dto.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-fixture-dto.g.ts",
  },
  {
    from: "place-order-fixture-resilience-predicates.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-fixture-resilience-predicates.g.ts",
  },
  {
    from: "place-order-v2-fixture-dto.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-v2-fixture-dto.g.ts",
  },
  {
    from: "place-order-v2-fixture-resilience-predicates.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/place-order-v2-fixture-resilience-predicates.g.ts",
  },
  {
    from: "deep-nest-fixture-dto.g.ts",
    to: "private/services/edge/tests/Unit/KeyCustodian/TypeSpecGrpcPredicate/Generated/deep-nest-fixture-dto.g.ts",
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
  // are EXCLUDED — namespace mismatch (tsp compile emits DcsvIo.D2.Private.Edge.KeyCustodian.* namespaces;
  // committed fixtures use DcsvIo.D2.Private.Edge.Tests.TypeSpecGrpcPredicate.Generated).
  // Handler interfaces and DTOs are governed by predicate-byte-parity.test.ts (which calls
  // emitHandlerInterface / emitCsharpDtos directly with the fixture namespace) and
  // nested-model-grpc-byte-parity.test.ts. Never scatter these from $onEmit output.

  // NOTE: OpenAPI (TypeSpecOpenApi/Generated/), SSE (TypeSpecSse/Generated/),
  // route registrations (TypeSpecRoute/Generated/), and all other gRPC service /
  // transport-mapper / C# DTO files are EXCLUDED for similar namespace-mismatch
  // reasons. Update them via the respective integration test suites.

  // NOTE: WireVersion.g.cs is EXCLUDED — namespace-sensitive
  // (emitted in proto-csharp-namespace D2.Services.Protos.KeyCustodian.V2Alpha;
  // tsp compile output differs from the byte-gate test-host fixture namespace).
  // Committed home: TypeSpecGrpc/Generated/WireVersion.g.cs.
  // Update via byteParity_WireVersionConstant_CommittedFixtureIdentical in
  // proto-grpc-byte-parity.test.ts (call emitWireVersionConstant directly).

  // NOTE: wire-identity.manifest.g.json is EXCLUDED — namespace-sensitive
  // (emitted alongside WireVersion.g.cs in the proto-csharp-namespace context;
  // the tsp compile output is co-located with the namespace-sensitive C# fixtures).
  // Committed home: TypeSpecGrpc/Generated/wire-identity.manifest.g.json.
  // Update via the wire-manifest-emitter.test.ts unit tests (emitWireIdentityManifest
  // directly) and proto-grpc-emit.integration.test.ts agree-by-construction assertion.
];

// ---------------------------------------------------------------------------
// Audit package COPY subset (standalone PingAudit + Edge bridge + client).
// ---------------------------------------------------------------------------

/** @type {ReadonlyArray<CopyRow>} */
const AUDIT_COPY = [
  // ---- PingAudit DTOs (Client.Ping concern) ----
  {
    from: "PingAuditInput.g.cs",
    to: "private/services/audit/clients/dotnet/Ping/PingAuditInput.g.cs",
  },
  {
    from: "PingAuditOutput.g.cs",
    to: "private/services/audit/clients/dotnet/Ping/PingAuditOutput.g.cs",
  },

  // ---- Audit gRPC client surface (Client package) ----
  {
    from: "IAuditGrpcClient.g.cs",
    to: "private/services/audit/clients/dotnet/IAuditGrpcClient.g.cs",
  },
  {
    from: "AuditGrpcClient.g.cs",
    to: "private/services/audit/clients/dotnet/AuditGrpcClient.g.cs",
  },
  {
    from: "AuditGrpcClientsGenerated.g.cs",
    to: "private/services/audit/clients/dotnet/AuditGrpcClientsGenerated.g.cs",
  },
  {
    from: "PingAuditClientKeys.g.cs",
    to: "private/services/audit/clients/dotnet/PingAuditClientKeys.g.cs",
  },
  {
    from: "PingAuditClientMappers.g.cs",
    to: "private/services/audit/clients/dotnet/PingAuditClientMappers.g.cs",
  },

  // ---- Handler interface (Audit.App per-op folder) ----
  {
    from: "IPingAuditHandler.g.cs",
    to: "private/services/audit/app/Application/Handlers/Queries/PingAudit/IPingAuditHandler.g.cs",
  },

  // ---- Thin gRPC service + transport mappers (Audit.Api) ----
  {
    from: "AuditPingService.g.cs",
    to: "private/services/audit/api/Grpc/AuditPingService.g.cs",
  },
  {
    from: "PingAuditTransportMappers.g.cs",
    to: "private/services/audit/api/Mappers/PingAuditTransportMappers.g.cs",
  },

  // ---- Proto (Audit.Api Protos; Client owns Grpc.Tools Both) ----
  {
    from: "audit_ping_ping_audit.g.proto",
    to: "private/services/audit/api/Protos/audit_ping_ping_audit.g.proto",
  },

  // ---- Edge HTTP→gRPC bridge (Edge.Api.Bridges.Audit) ----
  {
    from: "PingAuditBridgeRegistration.g.cs",
    to: "private/services/edge/api/Bridges/Audit/PingAuditBridgeRegistration.g.cs",
  },
  {
    from: "AuditBridgeRegistrations.g.cs",
    to: "private/services/edge/api/Bridges/Audit/AuditBridgeRegistrations.g.cs",
  },
];

/** @type {ReadonlyArray<PackageSpec>} */
const PACKAGES = [
  {
    name: "key-custodian",
    configRel: join("key-custodian", "tspconfig.yaml"),
    projectDirRel: "key-custodian",
    mainImports: [
      'import "./key-custodian.tsp";',
      'import "../fixtures/sign-shaped.tsp";',
      'import "../fixtures/temporal-shaped.tsp";',
      'import "../fixtures/enum-shaped.tsp";',
      'import "../fixtures/resilience-predicate-shaped.tsp";',
    ],
    copy: KEY_CUSTODIAN_COPY,
  },
  {
    name: "audit",
    configRel: join("audit", "tspconfig.yaml"),
    projectDirRel: "audit",
    mainImports: ['import "./audit.tsp";'],
    copy: AUDIT_COPY,
  },
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

/**
 * Copy one package's COPY_MANIFEST subset; fail-loud if any source is missing.
 * @param {string} packageName
 * @param {readonly CopyRow[]} rows
 * @returns {number} files copied
 */
function copyPackageSubset(packageName, rows) {
  let copied = 0;
  const missing = [];

  console.log(`\nCOPY subset [${packageName}] (${rows.length} rows)…`);

  for (const { from, to } of rows) {
    const src = join(DIST_GENERATED, from);
    const dest = join(REPO_ROOT, to);

    if (!existsSync(src)) {
      missing.push(src);
      continue;
    }

    mkdirSync(dirname(dest), { recursive: true });
    copyFileSync(src, dest);
    console.log(`  ${from}`);
    console.log(`    → ${to}`);
    copied++;
  }

  if (missing.length > 0) {
    console.error(
      `\nERROR: package [${packageName}] expected outputs missing from dist/generated/:`,
    );

    for (const m of missing) console.error(`  ${m}`);

    console.error(
      "\nThe compile succeeded but some expected outputs were not produced.",
    );
    console.error(
      "Check whether the corresponding .tsp definitions were removed or renamed,",
    );
    console.error("or whether COPY_MANIFEST rows for this package are stale.");
    process.exit(1);
  }

  return copied;
}

/**
 * Compile one package and COPY its subset immediately (sequence lock).
 * @param {PackageSpec} pkg
 * @returns {number} files copied
 */
function compileAndCopyPackage(pkg) {
  const projectDir = join(CONTRACTS_DIR, pkg.projectDirRel);
  const configPath = join(CONTRACTS_DIR, pkg.configRel);
  const tempMain = join(projectDir, "main.tsp");

  if (!existsSync(configPath)) {
    console.error(`ERROR: tspconfig not found for package [${pkg.name}]:`);
    console.error(`  ${configPath}`);
    process.exit(1);
  }

  if (!existsSync(projectDir)) {
    console.error(`ERROR: project dir missing for package [${pkg.name}]:`);
    console.error(`  ${projectDir}`);
    process.exit(1);
  }

  console.log(`\n=== Package [${pkg.name}] — tsp compile ===`);
  console.log(`  config: ${pkg.configRel}`);

  let tempMainCreated = false;

  try {
    // `tsp compile <dir>` requires main.tsp; imports mirror tspconfig imports.
    writeFileSync(tempMain, [...pkg.mainImports, ""].join("\n"), "utf8");
    tempMainCreated = true;

    const tspResult = spawnSync(
      process.execPath,
      [TSP_JS, "compile", projectDir, "--config", configPath],
      {
        cwd: projectDir,
        encoding: "utf8",
        stdio: "pipe",
      },
    );

    if (tspResult.stdout) process.stdout.write(tspResult.stdout);
    if (tspResult.stderr) process.stderr.write(tspResult.stderr);

    if (tspResult.status !== 0) {
      console.error(
        `\nERROR: tsp compile failed for package [${pkg.name}] (exit ${tspResult.status}).`,
      );
      process.exit(tspResult.status ?? 1);
    }

    console.log(`Compilation succeeded for [${pkg.name}].`);
  } finally {
    if (tempMainCreated) {
      try {
        unlinkSync(tempMain);
      } catch {
        console.warn(`WARN: could not remove temp ${tempMain}`);
      }
    }
  }

  // COPY immediately so the next package's compile cannot clobber these sources.
  return copyPackageSubset(pkg.name, pkg.copy);
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

console.log(
  "regen-typespec-emitters: multi-package N× (compile + COPY subset) …\n",
);

// Validate prerequisites.
if (!existsSync(TSP_JS)) {
  console.error(`ERROR: TypeSpec compiler not found at:\n  ${TSP_JS}`);
  console.error("Run pnpm install in the emitter package first.");
  process.exit(1);
}

// Create temp junctions — removed in the finally block.
let junctionContractsNmCreated = false;
let junctionPublicContractsNmCreated = false;
let junctionSelfRefCreated = false;
let junctionFixturesCreated = false;
let totalCopied = 0;

try {
  // ---- 1. Junction: private/contracts/typespec/node_modules → emitters/node_modules ----
  //
  // TypeSpec's module resolver walks up from the imported .tsp file to find
  // node_modules. private/contracts/typespec/ has none, so we bridge it to the
  // emitter package's node_modules. NTFS junctions do not require admin on Windows.
  removeSafe(JUNCTION_CONTRACTS_NM);
  symlinkSync(EMITTERS_NM, JUNCTION_CONTRACTS_NM, "junction");
  junctionContractsNmCreated = true;

  // ---- 1b. Same bridge for public/contracts/typespec (shared fixtures + common/) ----
  //
  // Product packages co-import public fixture .tsp files; resolution walks from
  // the fixture path under public/, which also needs node_modules.
  if (existsSync(PUBLIC_CONTRACTS_TYPESPEC)) {
    removeSafe(JUNCTION_PUBLIC_CONTRACTS_NM);
    symlinkSync(EMITTERS_NM, JUNCTION_PUBLIC_CONTRACTS_NM, "junction");
    junctionPublicContractsNmCreated = true;
  }

  // ---- 2. Junction: emitters/node_modules/@dcsv-io/d2-typespec-emitters → emitters/ ----
  //
  // The emitter itself is a workspace package — it's not installed in its own
  // node_modules/@dcsv-io/d2-typespec-emitters/. tsp compile resolves the emitter
  // declared in tspconfig.yaml's `emit:` array via normal module resolution, so
  // a self-referencing junction is required.
  const dcsvIoNmDir = join(EMITTERS_NM, "@dcsv-io");

  if (!existsSync(dcsvIoNmDir)) mkdirSync(dcsvIoNmDir, { recursive: true });

  removeSafe(JUNCTION_SELF_REF);
  symlinkSync(EMITTERS_DIR, JUNCTION_SELF_REF, "junction");
  junctionSelfRefCreated = true;

  // ---- 2b. Junction: private/.../fixtures → public/.../fixtures (co-compile) ----
  //
  // KC tspconfig still imports `../fixtures/*.tsp` under the private contracts
  // tree; shared fixture specs live under public/contracts/typespec/fixtures.
  if (
    existsSync(PUBLIC_FIXTURES_DIR) &&
    !existsSync(JUNCTION_PRIVATE_FIXTURES)
  ) {
    symlinkSync(PUBLIC_FIXTURES_DIR, JUNCTION_PRIVATE_FIXTURES, "junction");
    junctionFixturesCreated = true;
  }

  // ---- 3. Per-package: compile → COPY subset (never multi-compile then one COPY) ----
  for (const pkg of PACKAGES) {
    totalCopied += compileAndCopyPackage(pkg);
  }
} finally {
  // ---- 4. Unconditional cleanup ----
  if (junctionFixturesCreated) {
    try {
      removeSafe(JUNCTION_PRIVATE_FIXTURES);
    } catch {
      console.warn(
        `WARN: could not remove junction ${JUNCTION_PRIVATE_FIXTURES}`,
      );
    }
  }

  if (junctionSelfRefCreated) {
    try {
      removeSafe(JUNCTION_SELF_REF);
    } catch {
      console.warn(`WARN: could not remove junction ${JUNCTION_SELF_REF}`);
    }
  }

  if (junctionPublicContractsNmCreated) {
    try {
      removeSafe(JUNCTION_PUBLIC_CONTRACTS_NM);
    } catch {
      console.warn(
        `WARN: could not remove junction ${JUNCTION_PUBLIC_CONTRACTS_NM}`,
      );
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

console.log(
  `\n✓ Regenerated ${totalCopied} committed file${totalCopied === 1 ? "" : "s"} across ${PACKAGES.length} package${PACKAGES.length === 1 ? "" : "s"}.`,
);
console.log(
  "  Run `pnpm --filter @dcsv-io/d2-typespec-emitters test` to confirm byte-gate tests pass.",
);
