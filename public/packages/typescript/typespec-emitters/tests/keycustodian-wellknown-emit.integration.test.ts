// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Integration + byte-gate for the REAL KeyCustodian well-known surface.
//
// Compiles the actual contracts/typespec/key-custodian/key-custodian.tsp through
// the test-host with the real-KC csharp namespaces and asserts that:
//   1. getJwks emits a MapGet("/.well-known/jwks.json") harmless route delegating
//      to IKeyCustodianApi.GetJwksAsync.
//   2. getOidcConfiguration emits a MapGet("/.well-known/openid-configuration")
//      harmless route delegating to IKeyCustodianApi.GetOidcConfigurationAsync.
//   3. GetOidcConfigurationOutput.g.cs carries [property: JsonPropertyName("jwks_uri")]
//      (and the other three snake_case OIDC keys), while `issuer` carries NO
//      attribute (its wire name equals the default) — the @encodedName emitter ext.
//   4. Each emitted artifact is byte-identical to its committed home (so a future
//      emitter or .tsp drift fails loudly here).

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
  name: "@d2/typespec-decorators",
  packageRoot: await findTestPackageRoot(
    new URL(
      "../node_modules/@d2/typespec-decorators/package.json",
      import.meta.url,
    ).href,
  ),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

const D2EmitterTestLibrary = createTestLibrary({
  name: "@d2/typespec-emitters",
  packageRoot: await findTestPackageRoot(import.meta.url),
  jsFileFolder: "dist",
  typespecFileFolder: "lib",
});

// The real-KC csharp options (mirrors contracts/typespec/tspconfig.yaml).
const KC_OPTIONS = {
  "csharp-namespace": "D2.Edge.Tests.TypeSpecDto.Generated",
  "csharp-clients-namespace": "D2.Edge.KeyCustodian.Client",
  "csharp-app-namespace-base": "D2.Edge.KeyCustodian.App.Application.Handlers",
  "proto-package": "d2.keycustodian.v2alpha",
  "proto-csharp-namespace": "D2.Services.Protos.KeyCustodian.V2Alpha",
  "grpc-service-namespace": "D2.Edge.KeyCustodian.Client.Grpc",
  "process-kind-by-module": { KeyCustodian: "edge-module" },
  "csharp-routes-namespace": {
    KeyCustodian: "D2.Edge.Api.Routes.KeyCustodian",
  },
};

const _REPO = findRepoRoot(import.meta.url);

function readCommitted(...parts: string[]): string {
  return readFileSync(join(_REPO, ...parts), "utf8").replace(/\r\n/g, "\n");
}

/**
 * Normalize the banner's "Source spec:" line. The test-host names the input
 * "main.tsp" while the real regen names it the committed
 * contracts/typespec/key-custodian/key-custodian.tsp path; that single banner
 * line is the only by-construction difference. Stripping it lets the byte-gate
 * pin the generated CODE byte-for-byte (the part that matters) without coupling
 * to the test-host's synthetic input filename.
 */
function stripSpecBanner(s: string): string {
  return s
    .replace(/\r\n/g, "\n")
    .replace(/^\/\/\s+Source spec:.*$/m, "//   Source spec: <normalized>");
}

function getEmittedFile(
  host: Awaited<ReturnType<typeof createTestHost>>,
  suffix: string,
): string | undefined {
  const stored = (host as unknown as { fs?: Map<string, string> }).fs;
  if (!(stored instanceof Map)) return undefined;
  const key = [...stored.keys()].find((k) => k.endsWith(suffix));
  return key !== undefined ? stored.get(key) : undefined;
}

describe("keyCustodianWellKnown_RealTspCompile", () => {
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
      join(
        _REPO,
        "contracts",
        "typespec",
        "key-custodian",
        "key-custodian.tsp",
      ),
      "utf8",
    );
    host.addTypeSpecFile("main.tsp", tsp);
    // compileAndDiagnose runs the emitters + populates host.fs without throwing
    // on diagnostics (the real KC .tsp carries @versioned, which can warn under
    // the isolated test-host). The error-free assertion is made explicitly below.
    await host.compileAndDiagnose("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": KC_OPTIONS },
      outputDir: "testing:/out",
    });
  });

  it("compiles with zero error diagnostics", () => {
    const errors = host.program.diagnostics.filter(
      (d) => d.severity === "error",
    );
    expect(errors).toHaveLength(0);
  });

  it("getJwks → harmless MapGet at /.well-known/jwks.json delegating to IKeyCustodianApi", () => {
    const route = getEmittedFile(host, "GetJwksRouteRegistration.g.cs");
    expect(route).toBeDefined();
    expect(route).toContain("namespace D2.Edge.Api.Routes.KeyCustodian;");
    expect(route).toContain("MapGet(");
    expect(route).toContain('"/.well-known/jwks.json"');
    expect(route).toContain("MarkAsD2HarmlessEndpoint()");
    expect(route).toContain("facade.GetJwksAsync(input, ct)");
    // Harmless → no scope enforcement.
    expect(route).not.toContain("RequireAnyScope");
  });

  it("getOidcConfiguration → harmless MapGet at /.well-known/openid-configuration", () => {
    const route = getEmittedFile(
      host,
      "GetOidcConfigurationRouteRegistration.g.cs",
    );
    expect(route).toBeDefined();
    expect(route).toContain("MapGet(");
    expect(route).toContain('"/.well-known/openid-configuration"');
    expect(route).toContain("MarkAsD2HarmlessEndpoint()");
    expect(route).toContain("facade.GetOidcConfigurationAsync(input, ct)");
  });

  it("GetOidcConfigurationOutput carries snake_case [JsonPropertyName] on the 4 OIDC fields, none on issuer", () => {
    const dto = getEmittedFile(host, "GetOidcConfigurationOutput.g.cs");
    expect(dto).toBeDefined();
    expect(dto).toContain("using System.Text.Json.Serialization;");
    expect(dto).toContain(
      '[property: JsonPropertyName("jwks_uri")] string JwksUri',
    );
    expect(dto).toContain(
      '[property: JsonPropertyName("id_token_signing_alg_values_supported")] IReadOnlyList<string> IdTokenSigningAlgValuesSupported',
    );
    expect(dto).toContain(
      '[property: JsonPropertyName("response_types_supported")] IReadOnlyList<string> ResponseTypesSupported',
    );
    expect(dto).toContain(
      '[property: JsonPropertyName("subject_types_supported")] IReadOnlyList<string> SubjectTypesSupported',
    );
    // issuer's wire name equals the default — NO attribute.
    expect(dto).toContain("string Issuer");
    expect(dto).not.toMatch(/JsonPropertyName\("issuer"\)/);
  });
});

describe("keyCustodianWellKnown_ByteGate_CommittedArtifactsIdentical", () => {
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
      join(
        _REPO,
        "contracts",
        "typespec",
        "key-custodian",
        "key-custodian.tsp",
      ),
      "utf8",
    );
    host.addTypeSpecFile("main.tsp", tsp);
    // compileAndDiagnose runs the emitters + populates host.fs without throwing
    // on diagnostics (the real KC .tsp carries @versioned, which can warn under
    // the isolated test-host). The error-free assertion is made explicitly below.
    await host.compileAndDiagnose("main.tsp", {
      emit: ["@d2/typespec-emitters"],
      options: { "@d2/typespec-emitters": KC_OPTIONS },
      outputDir: "testing:/out",
    });
  });

  const cases: ReadonlyArray<{ emitted: string; committed: string[] }> = [
    {
      emitted: "GetOidcConfigurationOutput.g.cs",
      committed: [
        "server",
        "services",
        "edge",
        "key-custodian",
        "client",
        "OidcConfiguration",
        "GetOidcConfigurationOutput.g.cs",
      ],
    },
    {
      emitted: "GetOidcConfigurationInput.g.cs",
      committed: [
        "server",
        "services",
        "edge",
        "key-custodian",
        "client",
        "OidcConfiguration",
        "GetOidcConfigurationInput.g.cs",
      ],
    },
    {
      emitted: "GetJwksRouteRegistration.g.cs",
      committed: [
        "server",
        "services",
        "edge",
        "api",
        "Routes",
        "KeyCustodian",
        "GetJwksRouteRegistration.g.cs",
      ],
    },
    {
      emitted: "GetOidcConfigurationRouteRegistration.g.cs",
      committed: [
        "server",
        "services",
        "edge",
        "api",
        "Routes",
        "KeyCustodian",
        "GetOidcConfigurationRouteRegistration.g.cs",
      ],
    },
    {
      emitted: "IGetOidcConfigurationHandler.g.cs",
      committed: [
        "server",
        "services",
        "edge",
        "key-custodian",
        "app",
        "Application",
        "Handlers",
        "Queries",
        "GetOidcConfiguration",
        "IGetOidcConfigurationHandler.g.cs",
      ],
    },
  ];

  for (const c of cases) {
    it(`${c.emitted} is byte-identical to its committed home (modulo the banner source-spec path)`, () => {
      const emitted = getEmittedFile(host, c.emitted);
      expect(emitted, `${c.emitted} must be emitted`).toBeDefined();
      expect(stripSpecBanner(emitted!)).toBe(
        stripSpecBanner(readCommitted(...c.committed)),
      );
    });
  }

  // Deliberate-drift non-vacuity: mutate committed route fixture by one token
  // and prove the byte gate would fail (not a tautology comparing a buffer to itself).
  // Fail-without-fix (§2.3): without these not.toBe(drifted) assertions the
  // byte-identical cases alone can pass vacuously (buffer compared to itself);
  // remove the drift negatives and the §1.20 non-vacuity pin is gone.
  it("deliberate-drift: mutated GetJwksRouteRegistration does not match emitted", () => {
    const emitted = getEmittedFile(host, "GetJwksRouteRegistration.g.cs");
    expect(emitted).toBeDefined();
    const committed = stripSpecBanner(
      readCommitted(
        "server",
        "services",
        "edge",
        "api",
        "Routes",
        "KeyCustodian",
        "GetJwksRouteRegistration.g.cs",
      ),
    );
    const drifted = committed.replace(
      "namespace D2.Edge.Api.Routes.KeyCustodian;",
      "namespace D2.Edge.Api.Routes.Drifted;",
    );
    expect(drifted).not.toBe(committed);
    expect(stripSpecBanner(emitted!)).not.toBe(drifted);
  });

  it("deliberate-drift: mutated GetOidcConfigurationRouteRegistration does not match emitted", () => {
    const emitted = getEmittedFile(
      host,
      "GetOidcConfigurationRouteRegistration.g.cs",
    );
    expect(emitted).toBeDefined();
    const committed = stripSpecBanner(
      readCommitted(
        "server",
        "services",
        "edge",
        "api",
        "Routes",
        "KeyCustodian",
        "GetOidcConfigurationRouteRegistration.g.cs",
      ),
    );
    const drifted = committed.replace(
      "MapGetOidcConfigurationRoute",
      "MapGetOidcConfigurationRouteDrifted",
    );
    expect(drifted).not.toBe(committed);
    expect(stripSpecBanner(emitted!)).not.toBe(drifted);
  });
});
