// Copyright (c) DCSV. All rights reserved.
//
// One-time seeding tool: stamps per-package publish metadata (NuGet <Version> /
// <PackageId> / <Description> / <IsPackable> / README packaging; npm "files")
// and creates a CHANGELOG.md skeleton next to every CONSUMABLE manifest in the
// shared-library trees plus the in-process KeyCustodian client.
//
// Run once from the repo root: `node tools/scripts/seed-package-metadata.mjs`.
// Idempotent: re-running detects already-seeded manifests and skips them.

import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const REPO_ROOT = path.resolve(
  path.dirname(fileURLToPath(import.meta.url)),
  "..",
  "..",
);
const INITIAL_VERSION = "0.1.0";

// ---------------------------------------------------------------------------
// Curated per-package descriptions. Authored from each package's README summary
// (or its source where no README exists) — accurate per package, never templated.
// ---------------------------------------------------------------------------

/** @type {Record<string, string>} */
const DOTNET_DESCRIPTIONS = {
  "D2.Shared.AspNetCore":
    "Cross-cutting ASP.NET Core middleware and endpoint primitives for D2-WORX service composition roots (security headers, CORS, health endpoints, ProblemDetails, mTLS, infrastructure-path matching).",
  "D2.Shared.Auth.Abstractions":
    "Identity and authorization vocabulary plus consumer-side runtime contracts for D2-WORX inbound auth — value types, constants, and read-only interfaces.",
  "D2.Shared.AuthContext.Abstractions":
    "Read-only IAuthContext interface for reasoning about caller identity, organization, scopes, and impersonation context in D2-WORX.",
  "D2.Shared.Auth":
    "Inbound auth runtime for D2-WORX — JWT validation, JWKS snapshot management, session-liveness checking, and the DI composition root.",
  "D2.Shared.Auth.Events":
    "Cross-service auth-lifecycle event DTOs for D2-WORX — the published message types one service emits and others consume to refresh in-process state.",
  "D2.Shared.Auth.Grpc":
    "gRPC-transport binding for D2-WORX inbound auth — a server-side interceptor running JWT validation, session liveness, and per-method scope enforcement.",
  "D2.Shared.Auth.Http":
    "HTTP-transport binding for D2-WORX inbound auth — convention-based middleware running JWT validation, session liveness, RFC 7807 ProblemDetails, and per-endpoint scopes.",
  "D2.Shared.Auth.Outbound":
    "Caller-side outbound auth for D2-WORX cross-process calls — forwarded transaction tokens, mTLS workload certificates, and RFC 8693 token exchange.",
  "D2.Shared.Auth.Startup":
    "Deny-by-default auth endpoint boot guard for D2-WORX — fails host startup when any mapped endpoint lacks a declared auth intent.",
  "D2.Shared.Caching.Abstractions":
    "Shared abstractions for the D2-WORX cache stack — the ILocalCache / IDistributedCache / ITieredCache marker interfaces and the invalidation backplane contract.",
  "D2.Shared.Caching.Distributed.Redis":
    "Redis-backed IDistributedCache and cache-invalidation backplane implementation for D2-WORX, wrapping StackExchange.Redis.",
  "D2.Shared.Caching.Local.Default":
    "Default per-process ILocalCache implementation for D2-WORX, wrapping Microsoft.Extensions.Caching.Memory with in-process lock state.",
  "D2.Shared.Caching.Tiered":
    "Two-tier cache for D2-WORX composing an L1 ILocalCache and an L2 IDistributedCache into the ITieredCache interface.",
  "D2.Shared.Contacts":
    "Composable, self-redacting PII value objects for D2-WORX — immutable Create-constructed building blocks (name, demographics, professional, email, phone) that fold into host entities.",
  "D2.Shared.Contacts.EntityFrameworkCore":
    "Reusable Entity Framework Core mapping for the D2.Shared.Contacts value objects, applied via infra IEntityTypeConfiguration.",
  "D2.Shared.Context.Abstractions":
    "Spec-driven request-context primitives for D2-WORX — IRequestContext, MutableRequestContext, and the cross-hop propagation codecs.",
  "D2.Shared.DataGovernance.Abstractions":
    "GDPR anonymization markers and the engine seam for D2-WORX entity models — domain-safe, without EF Core or DI.",
  "D2.Shared.DataGovernance.EntityFrameworkCore":
    "EF Core wiring for D2-WORX GDPR anonymization — the fluent .Anonymize* API, the engine registration, and the startup model guard.",
  "D2.Shared.Encryption":
    "AES-256-GCM payload encryption with a JWKS-style multi-kid keyring for D2-WORX — a pure crypto primitive decoupled from key sources.",
  "D2.Shared.EntityFrameworkCore":
    "EF Core migration helpers for D2-WORX — declaring indexes on ComplexProperty member columns.",
  "D2.Shared.EntityFrameworkCore.Postgres":
    "PostgreSQL-backed EF Core DbContext wiring for D2-WORX with advisory-lock-guarded migrations and startup validation.",
  "D2.Shared.ErrorCodes.Category":
    "Foundational zero-dependency leaf exposing the closed ErrorCategory classification carried by every D2Result and error code in D2-WORX.",
  "D2.Shared.ErrorCodes.Registry":
    "Merged cross-catalog error-code registry for D2-WORX — a frozen code-to-metadata lookup aggregating every error-code spec.",
  "D2.Shared.Geo.Abstractions":
    "Strongly-typed ISO geo reference-data type surface for D2-WORX (countries, subdivisions, currencies, languages, locales, timezones) without catalog data.",
  "D2.Shared.Geo.Default":
    "The D2-WORX geo catalog data — per-entity instances, lookup tables, and nested static hierarchies bound into memory at process start.",
  "D2.Shared.Handler.Abstractions":
    "Domain-safe slice of the D2-WORX handler stack — IHandler, IHandlerContext, and HandlerOptions.",
  "D2.Shared.Handler":
    "BaseHandler for D2-WORX — the abstract base every handler inherits, providing scope pre-checks, OpenTelemetry activity and metrics, log scope, and a universal try/catch.",
  "D2.Shared.Handler.Repo":
    "EF-flavored BaseRepoHandler for D2-WORX — converts database exceptions captured during execution into typed D2Result failures.",
  "D2.Shared.Handler.Repo.Abstractions":
    "Vocabulary for repo-flavored D2-WORX handlers — the database-failure discrimination contract (unique, FK, deadlock, concurrency, connection), zero infrastructure dependencies.",
  "D2.Shared.Handler.Repo.Postgres":
    "PostgreSQL IDbExceptionClassifier implementation for D2-WORX, plugging Npgsql failure classification into BaseRepoHandler.",
  "D2.Shared.Headers.Amqp":
    "D2-WORX wire-protocol header constants applicable to the AMQP transport, code-generated from the shared headers spec.",
  "D2.Shared.Headers.Common":
    "Cross-transport D2-WORX wire-protocol header constants (headers that appear identically on multiple transports), code-generated from the shared headers spec.",
  "D2.Shared.Headers.Grpc":
    "D2-WORX wire-protocol header constants applicable to the gRPC transport, code-generated from the shared headers spec.",
  "D2.Shared.Headers.Http":
    "D2-WORX wire-protocol header constants applicable to the HTTP transport, code-generated from the shared headers spec.",
  "D2.Shared.I18n.Abstractions":
    "Domain-safe i18n slice for D2-WORX — the TKMessage primitive and the ITranslator interface, with zero external dependencies.",
  "D2.Shared.I18n":
    "Runtime translation library for D2-WORX — Translator, the env-driven SupportedLocales registry, and the AddD2I18n DI extension.",
  "D2.Shared.I18n.Keys":
    "Type-safe TK constants catalog for D2-WORX — one TKMessage per translation key, code-generated from the message catalog.",
  "D2.Shared.Location":
    "Hash-deduplicatable geographic value objects for D2-WORX — coordinates, street and admin addresses, deterministic identity hashes, and the postal-code validator contract.",
  "D2.Shared.Location.EntityFrameworkCore":
    "Reusable Entity Framework Core mapping for the D2.Shared.Location value objects, applied via infra IEntityTypeConfiguration.",
  "D2.Shared.Logging":
    "Serilog configuration, the [RedactData] PII-enforcement layer, and request-logging middleware for D2-WORX services.",
  "D2.Shared.Messaging.Abstractions":
    "Transport-agnostic messaging abstractions for D2-WORX — the [MqPub] / [MqSub] vocabulary, the message-bus contract, and DLQ failure-metadata wire shapes.",
  "D2.Shared.Messaging.RabbitMq":
    "Default RabbitMQ implementation of the D2-WORX messaging abstractions — publishing, subscribing, encryption frames, and dead-letter handling.",
  "D2.Shared.ProblemDetails.Abstractions":
    "RFC 7807 ProblemDetails wire-format catalog for D2-WORX — type-URI prefix, extension keys, per-status titles, and the TitleFor helper.",
  "D2.Shared.Resilience":
    "The D2-WORX resilience pipeline — retry, circuit breaker, singleflight, timeout, and concurrency rate-limiting as composable, caller-side, opt-in layers.",
  "D2.Shared.Result":
    "D2Result — the errors-as-values pattern for D2-WORX, replacing exception-based control flow with TKMessage-typed user-facing messages.",
  "D2.Shared.Result.Grpc":
    "Faithful in-memory to wire to in-memory D2Result round-trip for D2-WORX over the gRPC D2ResultProto response envelope.",
  "D2.Shared.ServiceDefaults":
    "Composition-root aggregator for D2-WORX — wires every shared library into AddD2ServiceDefaults / UseD2DefaultPipeline / MapD2DefaultEndpoints / RunD2ServiceAsync.",
  "D2.Shared.Telemetry":
    "OpenTelemetry SDK setup for D2-WORX — traces, metrics, logs, OTLP exporters, an IP-restricted Prometheus endpoint, and aggregation of every shared library's ActivitySource and Meter.",
  "D2.Shared.Time":
    "Deterministic timestamp handling for D2-WORX — a dependency-injected clock seam, temporal storage types, and NodaTime to PostgreSQL EF Core wiring.",
  "D2.Shared.Utilities":
    "Foundational boundary helpers for D2-WORX — Falsey/Truthy semantics, string cleaning, parse-or-default extensions, indexed env-var parsing, and JSON-cycle-safe serialization.",
  "D2.Shared.Validation.Abstractions":
    "Validation contract surface for D2-WORX — email, phone, and postal-code validator interfaces, shared field-length bounds, and the name/sex taxonomy enums.",
  "D2.Shared.Validation":
    "Default validators for D2-WORX — email, phone, and postal-code validation backed by libphonenumber-csharp and a ported postcode dataset.",
  "D2.Shared.WorkloadIdentity":
    "SPIFFE workload-identity value object and peer validator for D2-WORX mTLS — the subject-alternative-name a leaf certificate carries and the trust-domain grammar.",
  // The in-process KeyCustodian client (outside the shared tree).
  "D2.Edge.KeyCustodian.Clients":
    "In-process KeyCustodian client surface for D2-WORX — the transport DTOs and the IKeyCustodianApi module facade.",
};

/** @type {Record<string, string>} */
const TS_DESCRIPTIONS = {
  "@d2/auth-abstractions":
    "Auth-related constants for D2-WORX TypeScript consumers — the Scopes tree, AuthErrorCodes, AuthFailures factories, and JwtClaimTypes. Mirrors D2.Shared.Auth.Abstractions (.NET).",
  "@d2/auth-context-abstractions":
    "IAuthContext interface and supporting types for D2-WORX, code-generated from the auth-context spec. Mirrors D2.Shared.AuthContext.Abstractions (.NET).",
  "@d2/encryption-abstractions":
    "D2-WORX encryption-domain identifiers and encryption-frame binary-layout constants. Mirrors D2.Shared.Encryption (.NET), spec-driven.",
  "@d2/error-category":
    "Foundational zero-dependency leaf exporting the closed ErrorCategory classification carried by every D2Result and error code. Mirrors D2.Shared.ErrorCodes.Category (.NET).",
  "@d2/error-codes-registry":
    "Merged cross-catalog error-code registry for D2-WORX — a frozen code-to-metadata lookup aggregating every error-code spec. Mirrors D2.Shared.ErrorCodes.Registry (.NET).",
  "@d2/geo-abstractions":
    "Data-free ISO geo reference-data type surface for D2-WORX — interfaces, meta-records, and name-resolution primitives. Mirrors D2.Shared.Geo.Abstractions (.NET).",
  "@d2/geo-default":
    "The D2-WORX geo catalog data for TypeScript — per-entity records, lookup maps, and nested objects bound at process start. Mirrors D2.Shared.Geo.Default (.NET).",
  "@d2/grpc-client":
    "Singleton-per-process gRPC channel from the SvelteKit BFF to Edge for D2-WORX, with context-propagation and auth interceptors.",
  "@d2/headers-amqp":
    "D2-WORX wire-protocol header constants applicable to the AMQP transport. Mirrors D2.Shared.Headers.Amqp (.NET) at byte-equal wire values.",
  "@d2/headers-common":
    "Cross-transport D2-WORX wire-protocol header constants. Mirrors D2.Shared.Headers.Common (.NET) at byte-equal wire values.",
  "@d2/headers":
    "SvelteKit BFF-side glue for the D2-WORX BFF-to-Edge boundary — reads inbound headers into an IRequestContext and applies the route guards.",
  "@d2/headers-grpc":
    "D2-WORX wire-protocol header constants applicable to the gRPC transport. Mirrors D2.Shared.Headers.Grpc (.NET) at byte-equal wire values.",
  "@d2/headers-http":
    "D2-WORX wire-protocol header constants applicable to the HTTP transport. Mirrors D2.Shared.Headers.Http (.NET) at byte-equal wire values.",
  "@d2/i18n":
    "ITranslator interface, SupportedLocales registry, and the default Translator implementation for D2-WORX. Mirrors D2.Shared.I18n (.NET).",
  "@d2/i18n-abstractions":
    "Foundational zero-dependency i18n primitives for D2-WORX — the TKMessage shape and the tk() factory. Mirrors D2.Shared.I18n.Abstractions (.NET).",
  "@d2/i18n-keys":
    "Type-safe TK constants catalog for D2-WORX TypeScript consumers. Mirrors D2.Shared.I18n.Keys (.NET).",
  "@d2/logging":
    "Pino-backed ILogger interface, the markRedactedFields() PII helper, and sanitizedErrorRender() for D2-WORX. Mirrors D2.Shared.Logging (.NET).",
  "@d2/messaging-abstractions":
    "D2-WORX messaging-protocol wire identifiers — the DLQ failure-metadata shape and the closed cause-string catalog. Mirrors D2.Shared.Messaging.Abstractions (.NET).",
  "@d2/problem-details-abstractions":
    "Foundational zero-dependency RFC 7807 ProblemDetails wire-format catalog for D2-WORX. Mirrors D2.Shared.ProblemDetails.Abstractions (.NET).",
  "@d2/protos":
    "Buf-generated TypeScript modules and gRPC client stubs from the D2-WORX shared protos. Mirrors D2.Shared.Protos (.NET).",
  "@d2/request-context-abstractions":
    "IRequestContext interface, the cross-hop IPropagatedContext subset, and the propagation round-trip serializer for D2-WORX. Mirrors D2.Shared.Context.Abstractions (.NET).",
  "@d2/resilience":
    "Retry, circuit breaker, singleflight, timeout, rate-limiter, and composable pipeline for D2-WORX TypeScript. Mirrors D2.Shared.Resilience (.NET).",
  "@d2/result":
    "D2Result with semantic factories and combine/bubble helpers for D2-WORX TypeScript. Mirrors D2.Shared.Result (.NET) at a byte-identical wire.",
  "@d2/service-defaults":
    "One-call bundle composing @d2/logging, @d2/telemetry, and the D2Env loader for D2-WORX Node services. Mirrors D2.Shared.ServiceDefaults (.NET).",
  "@d2/telemetry":
    "One-call OpenTelemetry SDK bootstrap for D2-WORX Node services — traces, metrics, logs, OTLP exporters, and the W3C propagator stack. Mirrors D2.Shared.Telemetry (.NET).",
  "@d2/time":
    "Deterministic clock seam and temporal storage types for D2-WORX TypeScript. Mirrors D2.Shared.Time (.NET).",
  "@d2/utilities":
    "Boundary helpers for D2-WORX TypeScript — falsey/truthy semantics, string cleaning, parse-or-undefined helpers, indexed env-var array parsing, and regex guards. Mirrors D2.Shared.Utilities (.NET).",
  "@d2/validation-abstractions":
    "Validator contract surface for D2-WORX TypeScript — email, phone, and postal-code validator interfaces and the shared field-constraints catalog. Mirrors D2.Shared.Validation.Abstractions (.NET).",
  "@d2/validation":
    "Default validators for D2-WORX TypeScript — email, phone, and postal-code validation and normalization matching the .NET rules. Mirrors D2.Shared.Validation (.NET).",
};

// ---------------------------------------------------------------------------
// Inventory discovery.
// ---------------------------------------------------------------------------

/** Recursively collect files under a directory, skipping noisy dirs. */
function walk(dir, out = []) {
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (
        [
          "node_modules",
          "dist",
          "bin",
          "obj",
          "Generated",
          "spike-csharp",
        ].includes(entry.name)
      ) {
        continue;
      }

      walk(full, out);
    } else {
      out.push(full);
    }
  }

  return out;
}

const dotnetFiles = walk(path.join(REPO_ROOT, "server", "shared", "dotnet"));
const tsFiles = walk(path.join(REPO_ROOT, "server", "shared", "typescript"));

const dotnetConsumables = dotnetFiles
  .filter((f) => f.endsWith(".csproj"))
  .filter((f) => !f.endsWith("SourceGen.csproj"))
  .filter((f) => !/D2\.Shared\.Tests\.csproj$/.test(f));

const kcClient = path.join(
  REPO_ROOT,
  "server",
  "services",
  "edge",
  "key-custodian",
  "clients",
  "D2.Edge.KeyCustodian.Clients.csproj",
);

const tsConsumables = tsFiles
  .filter((f) => /package\.json$/.test(f))
  .filter(
    (f) =>
      !/(typespec-decorators|typespec-emitters|contract-tests)[\\/]package\.json$/.test(
        f,
      ),
  );

// ---------------------------------------------------------------------------
// Seeding.
// ---------------------------------------------------------------------------

const CHANGELOG_NL = "\n";

function changelogSkeleton(packageName) {
  return [
    `# Changelog — ${packageName}`,
    "",
    "All notable changes to this package are documented here. The format follows",
    "Keep a Changelog, and this package adheres to Semantic Versioning.",
    "",
    "## [Unreleased]",
    "",
    "### Wire-breaking",
    "",
    "### API-breaking",
    "",
    "### Added",
    "",
    "### Fixed",
    "",
  ].join(CHANGELOG_NL);
}

function writeChangelog(dir, packageName) {
  const target = path.join(dir, "CHANGELOG.md");

  if (fs.existsSync(target)) {
    return false;
  }

  fs.writeFileSync(target, changelogSkeleton(packageName), "utf8");
  return true;
}

/** Detect the line ending used by an existing file (default LF). */
function detectEol(text) {
  return text.includes("\r\n") ? "\r\n" : "\n";
}

/** Inject the NuGet packaging PropertyGroup + README pack item into a .csproj. */
function seedCsproj(
  csprojPath,
  { packageId, description, inlineSharedMetadata },
) {
  let text = fs.readFileSync(csprojPath, "utf8");

  if (text.includes("<PackageId>")) {
    return false;
  }

  const eol = detectEol(text);
  const dir = path.dirname(csprojPath);
  const hasReadme = fs.existsSync(path.join(dir, "README.md"));

  const propLines = [];
  propLines.push(
    "  <!-- Publish metadata (per-package semver + NuGet identity). -->",
  );
  propLines.push("  <PropertyGroup>");
  propLines.push(`    <Version>${INITIAL_VERSION}</Version>`);
  propLines.push(`    <PackageId>${packageId}</PackageId>`);
  propLines.push(`    <Description>${description}</Description>`);
  propLines.push("    <IsPackable>true</IsPackable>");

  if (inlineSharedMetadata) {
    // Outside server/shared/dotnet/, so the shared props do not apply — stamp
    // the shared authorship/license/repo metadata inline.
    propLines.push("    <Authors>DCSV</Authors>");
    propLines.push("    <Company>DCSV</Company>");
    propLines.push("    <Product>D2-WORX</Product>");
    propLines.push(
      "    <RepositoryUrl>https://github.com/DCSV-io/D2-WORX</RepositoryUrl>",
    );
    propLines.push("    <RepositoryType>git</RepositoryType>");
    propLines.push(
      "    <PackageProjectUrl>https://github.com/DCSV-io/D2-WORX</PackageProjectUrl>",
    );
    propLines.push("    <PackageLicenseFile>LICENSE.md</PackageLicenseFile>");
  }

  if (hasReadme) {
    propLines.push("    <PackageReadmeFile>README.md</PackageReadmeFile>");
  }

  propLines.push("  </PropertyGroup>");

  // ItemGroup for packed files (README always; LICENSE only when inline since
  // the shared props already pack LICENSE for tree-resident packages).
  const itemLines = [];
  const packItems = [];

  if (hasReadme) {
    packItems.push(
      '    <None Include="README.md" Pack="true" PackagePath="\\" />',
    );
  }

  if (inlineSharedMetadata) {
    packItems.push(
      '    <None Include="$(D2RepoRoot)LICENSE.md" Pack="true" PackagePath="\\" Visible="false" />',
    );
  }

  if (packItems.length > 0) {
    itemLines.push("  <ItemGroup>");
    itemLines.push(...packItems);
    itemLines.push("  </ItemGroup>");
  }

  const block = [
    ...propLines,
    ...(itemLines.length ? ["", ...itemLines] : []),
  ].join(eol);

  // Insert right before the closing </Project>.
  const idx = text.lastIndexOf("</Project>");

  if (idx === -1) {
    throw new Error(`No </Project> in ${csprojPath}`);
  }

  // Ensure a blank line precedes the inserted block for readability.
  const before = text.slice(0, idx).replace(/\s*$/, eol + eol);
  text = before + block + eol + "</Project>" + eol;

  fs.writeFileSync(csprojPath, text, "utf8");
  return true;
}

/** Add "files": ["dist"] to a package.json, preserving key order + EOL. */
function seedPackageJson(pkgPath) {
  const text = fs.readFileSync(pkgPath, "utf8");
  const eol = detectEol(text);
  const json = JSON.parse(text);

  if (json.files) {
    return false;
  }

  // Rebuild with "files" inserted right after "exports" (or after "types").
  const entries = Object.entries(json);
  const out = {};

  for (const [k, v] of entries) {
    out[k] = v;

    if ((k === "exports" || (k === "types" && !json.exports)) && !out.files) {
      out.files = ["dist"];
    }
  }

  if (!out.files) {
    out.files = ["dist"];
  }

  fs.writeFileSync(pkgPath, JSON.stringify(out, null, 2) + eol, "utf8");
  return true;
}

// ---------------------------------------------------------------------------
// Run.
// ---------------------------------------------------------------------------

let csSeeded = 0;
let csChangelogs = 0;
const missingDesc = [];

for (const csprojPath of dotnetConsumables) {
  const packageId = path.basename(csprojPath, ".csproj");
  const description = DOTNET_DESCRIPTIONS[packageId];

  if (!description) {
    missingDesc.push(packageId);
    continue;
  }

  if (
    seedCsproj(csprojPath, {
      packageId,
      description,
      inlineSharedMetadata: false,
    })
  ) {
    csSeeded++;
  }

  if (writeChangelog(path.dirname(csprojPath), packageId)) {
    csChangelogs++;
  }
}

// KeyCustodian client — inline shared metadata (outside the shared-props tree).
{
  const packageId = "D2.Edge.KeyCustodian.Clients";
  const description = DOTNET_DESCRIPTIONS[packageId];

  if (
    seedCsproj(kcClient, { packageId, description, inlineSharedMetadata: true })
  ) {
    csSeeded++;
  }

  if (writeChangelog(path.dirname(kcClient), packageId)) {
    csChangelogs++;
  }
}

let tsSeeded = 0;
let tsChangelogs = 0;

for (const pkgPath of tsConsumables) {
  const name = JSON.parse(fs.readFileSync(pkgPath, "utf8")).name;

  if (!TS_DESCRIPTIONS[name]) {
    missingDesc.push(name);
  }

  if (seedPackageJson(pkgPath)) {
    tsSeeded++;
  }

  if (writeChangelog(path.dirname(pkgPath), name)) {
    tsChangelogs++;
  }
}

console.log(
  JSON.stringify(
    {
      dotnetConsumables: dotnetConsumables.length + 1, // + KC client
      csSeeded,
      csChangelogs,
      tsConsumables: tsConsumables.length,
      tsSeeded,
      tsChangelogs,
      totalChangelogs: csChangelogs + tsChangelogs,
      missingDesc,
    },
    null,
    2,
  ),
);
