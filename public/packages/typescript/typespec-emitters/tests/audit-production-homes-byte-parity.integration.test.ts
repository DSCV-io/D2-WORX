// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// §26.5.1 standing byte-parity for every Audit + Edge Bridges/Audit production
// COPY destination (AUDIT_COPY in tools/scripts/regen-typespec-emitters.mjs).
// Compiles the real contracts/typespec/audit/audit.tsp through the test-host
// with production csharp namespaces and asserts regenerate ↔ committed identity
// (modulo Source-spec banner path). Deliberate-drift negatives keep the gate
// non-vacuous.

import { describe, it, expect, beforeAll } from "vitest";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import {
  createTestLibrary,
  createTestHost,
  findTestPackageRoot,
} from "@typespec/compiler/testing";
import { HttpTestLibrary } from "@typespec/http/testing";
import { VersioningTestLibrary } from "@typespec/versioning/testing";
import { findRepoRoot } from "./repo-root.js";

const D2DecoratorTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@dcsv-io/d2-typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const D2EmitterTestLibrary = createTestLibrary({
  name: "@dcsv-io/d2-typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

/** Production Audit emit options (mirrors contracts/typespec/audit/tspconfig.yaml). */
const AUDIT_OPTIONS = {
  "csharp-clients-namespace": "DcsvIo.D2.Private.Audit.Client",
  "csharp-app-namespace-base":
    "DcsvIo.D2.Private.Audit.App.Application.Handlers",
  "proto-package": "d2.audit.v2alpha",
  "proto-csharp-namespace": "D2.Services.Protos.Audit.V2Alpha",
  "grpc-service-namespace": "DcsvIo.D2.Private.Audit.Api.Grpc",
  "process-kind-by-module": { Audit: "standalone" },
  "csharp-bridge-namespace": {
    Audit: "DcsvIo.D2.Private.Edge.Api.Bridges.Audit",
  },
};

const _REPO = findRepoRoot(import.meta.url);

function readCommitted(...parts: string[]): string {
  return readFileSync(join(_REPO, ...parts), "utf8").replace(/\r\n/g, "\n");
}

/**
 * Normalize the banner's "Source spec:" line. The test-host names the input
 * "main.tsp" while real regen names the committed contracts path.
 */
function stripSpecBanner(s: string): string {
  return s
    .replace(/\r\n/g, "\n")
    .replace(/^\/\/\s+Source spec:.*$/m, "//   Source spec: <normalized>");
}

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  fileName: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  // Exact basename match — endsWith("AuditGrpcClient.g.cs") would also hit
  // "IAuditGrpcClient.g.cs".
  const key = [...stored.keys()].find((k) => {
    const base = k.replace(/\\/g, "/").split("/").pop();
    return base === fileName;
  });
  return key !== undefined ? stored.get(key) : undefined;
}

/** Every AUDIT_COPY destination under production homes (13 rows). */
const AUDIT_PRODUCTION_HOMES: ReadonlyArray<{
  emitted: string;
  committed: string[];
}> = [
  {
    emitted: "PingAuditInput.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "Ping",
      "PingAuditInput.g.cs",
    ],
  },
  {
    emitted: "PingAuditOutput.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "Ping",
      "PingAuditOutput.g.cs",
    ],
  },
  {
    emitted: "IAuditGrpcClient.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "IAuditGrpcClient.g.cs",
    ],
  },
  {
    emitted: "AuditGrpcClient.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "AuditGrpcClient.g.cs",
    ],
  },
  {
    emitted: "AuditGrpcClientsGenerated.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "AuditGrpcClientsGenerated.g.cs",
    ],
  },
  {
    emitted: "PingAuditClientKeys.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "PingAuditClientKeys.g.cs",
    ],
  },
  {
    emitted: "PingAuditClientMappers.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "clients",
      "dotnet",
      "PingAuditClientMappers.g.cs",
    ],
  },
  {
    emitted: "IPingAuditHandler.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "app",
      "Application",
      "Handlers",
      "Queries",
      "PingAudit",
      "IPingAuditHandler.g.cs",
    ],
  },
  {
    emitted: "AuditPingService.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "api",
      "Grpc",
      "AuditPingService.g.cs",
    ],
  },
  {
    emitted: "PingAuditTransportMappers.g.cs",
    committed: [
      "private",
      "services",
      "audit",
      "api",
      "Mappers",
      "PingAuditTransportMappers.g.cs",
    ],
  },
  {
    emitted: "audit_ping_ping_audit.g.proto",
    committed: [
      "private",
      "services",
      "audit",
      "api",
      "Protos",
      "audit_ping_ping_audit.g.proto",
    ],
  },
  {
    emitted: "PingAuditBridgeRegistration.g.cs",
    committed: [
      "private",
      "services",
      "edge",
      "api",
      "Bridges",
      "Audit",
      "PingAuditBridgeRegistration.g.cs",
    ],
  },
  {
    emitted: "AuditBridgeRegistrations.g.cs",
    committed: [
      "private",
      "services",
      "edge",
      "api",
      "Bridges",
      "Audit",
      "AuditBridgeRegistrations.g.cs",
    ],
  },
];

describe("auditProductionHomes_RealTspCompile", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        VersioningTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
    const tsp = readFileSync(
      join(_REPO, "private", "contracts", "typespec", "audit", "audit.tsp"),
      "utf8",
    );
    host.addTypeSpecFile("main.tsp", tsp);
    await host.compileAndDiagnose("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: { "@dcsv-io/d2-typespec-emitters": AUDIT_OPTIONS },
      outputDir: "testing:/out",
    });
  });

  it("compiles with zero error diagnostics", () => {
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);
  });

  it("PingAudit bridge emits RequireAnyScope(Scopes.Internal.Audit.Ping) — not Harmless", () => {
    const bridge = getEmittedFile(host, "PingAuditBridgeRegistration.g.cs");
    expect(bridge).toBeDefined();
    expect(bridge).toContain("RequireAnyScope(Scopes.Internal.Audit.Ping)");
    expect(bridge).toContain("using DcsvIo.D2.Auth.Abstractions;");
    expect(bridge).not.toContain("MarkAsD2HarmlessEndpoint");
    expect(bridge).not.toContain('RequireAnyScope("');
  });
});

describe("auditProductionHomes_ByteGate_CommittedArtifactsIdentical", () => {
  let host: Awaited<ReturnType<typeof createTestHost>>;

  beforeAll(async () => {
    host = await createTestHost({
      libraries: [
        HttpTestLibrary,
        VersioningTestLibrary,
        D2DecoratorTestLibrary,
        D2EmitterTestLibrary,
      ],
    });
    const tsp = readFileSync(
      join(_REPO, "private", "contracts", "typespec", "audit", "audit.tsp"),
      "utf8",
    );
    host.addTypeSpecFile("main.tsp", tsp);
    await host.compileAndDiagnose("main.tsp", {
      emit: ["@dcsv-io/d2-typespec-emitters"],
      options: { "@dcsv-io/d2-typespec-emitters": AUDIT_OPTIONS },
      outputDir: "testing:/out",
    });
  });

  for (const c of AUDIT_PRODUCTION_HOMES) {
    it(`${c.emitted} is byte-identical to its committed home (modulo banner source-spec path)`, () => {
      const emitted = getEmittedFile(host, c.emitted);
      expect(emitted, `${c.emitted} must be emitted`).toBeDefined();
      expect(stripSpecBanner(emitted!)).toBe(
        stripSpecBanner(readCommitted(...c.committed)),
      );
    });
  }

  // Deliberate-drift non-vacuity (§1.20 / §26.5.1).
  it("deliberate-drift: mutated PingAuditBridgeRegistration does not match emitted", () => {
    const emitted = getEmittedFile(host, "PingAuditBridgeRegistration.g.cs");
    expect(emitted).toBeDefined();
    const committed = stripSpecBanner(
      readCommitted(
        "private",
        "services",
        "edge",
        "api",
        "Bridges",
        "Audit",
        "PingAuditBridgeRegistration.g.cs",
      ),
    );
    const drifted = committed.replace(
      "namespace DcsvIo.D2.Private.Edge.Api.Bridges.Audit;",
      "namespace DcsvIo.D2.Private.Edge.Api.Bridges.Drifted;",
    );
    expect(drifted).not.toBe(committed);
    expect(stripSpecBanner(emitted!)).not.toBe(drifted);
  });

  it("deliberate-drift: mutated AuditGrpcClientsGenerated does not match emitted", () => {
    const emitted = getEmittedFile(host, "AuditGrpcClientsGenerated.g.cs");
    expect(emitted).toBeDefined();
    const committed = stripSpecBanner(
      readCommitted(
        "private",
        "services",
        "audit",
        "clients",
        "dotnet",
        "AuditGrpcClientsGenerated.g.cs",
      ),
    );
    const drifted = committed.replace(
      "AddD2AuditGrpcClients",
      "AddD2AuditGrpcClientsDRIFTED",
    );
    expect(drifted).not.toBe(committed);
    expect(stripSpecBanner(emitted!)).not.toBe(drifted);
  });

  it("deliberate-drift: mutated IPingAuditHandler does not match emitted", () => {
    const emitted = getEmittedFile(host, "IPingAuditHandler.g.cs");
    expect(emitted).toBeDefined();
    const committed = stripSpecBanner(
      readCommitted(
        "private",
        "services",
        "audit",
        "app",
        "Application",
        "Handlers",
        "Queries",
        "PingAudit",
        "IPingAuditHandler.g.cs",
      ),
    );
    const drifted = committed.replace(
      "IPingAuditHandler",
      "IPingAuditHandlerDRIFTED",
    );
    expect(drifted).not.toBe(committed);
    expect(stripSpecBanner(emitted!)).not.toBe(drifted);
  });
});
