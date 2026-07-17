// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Unit tests for the wire-identity manifest emitter.
//
// Covers:
//   emitWireIdentityManifest — records protoPackage, protoCsharpNamespace,
//     generation, stability, channel, x-d2-generated-by; valid JSON;
//     asserts NO published package name key is present (pins the
//     no-premature-package-name decision).

import { describe, it, expect } from "vitest";
import { emitWireIdentityManifest } from "../src/lib/wire-manifest-emitter.js";
import { parseChannel } from "../src/lib/wire-channel.js";

const PROTO_PACKAGE = "d2.sample.v2alpha";
const PROTO_CSHARP_NS = "D2.Services.Protos.Sample.V2Alpha";
const ALPHA_CHANNEL = parseChannel(PROTO_PACKAGE)!;

const BETA_PACKAGE = "d2.geo.v2beta";
const BETA_CHANNEL = parseChannel(BETA_PACKAGE)!;

// ---------------------------------------------------------------------------
// Identity facts recorded
// ---------------------------------------------------------------------------

describe("emitWireIdentityManifest_AlphaChannel_RecordsIdentityFacts", () => {
  it("records protoPackage", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["protoPackage"]).toBe(PROTO_PACKAGE);
  });

  it("records protoCsharpNamespace", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["protoCsharpNamespace"]).toBe(PROTO_CSHARP_NS);
  });

  it("records generation as number 2", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["generation"]).toBe(2);
  });

  it("records stability as 'alpha'", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["stability"]).toBe("alpha");
  });

  it("records channel as 'v2alpha'", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["channel"]).toBe("v2alpha");
  });

  it("records x-d2-generated-by provenance", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["x-d2-generated-by"]).toBe("@dcsv-io/d2-typespec-emitters");
  });
});

// ---------------------------------------------------------------------------
// Valid JSON
// ---------------------------------------------------------------------------

describe("emitWireIdentityManifest_OutputIsValidJson", () => {
  it("JSON.parse round-trips without throwing", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    expect(() => JSON.parse(file.content)).not.toThrow();
  });
});

// ---------------------------------------------------------------------------
// No premature package name (pins the no-package-name-baking decision)
// ---------------------------------------------------------------------------

describe("emitWireIdentityManifest_NoPrematurePackageName", () => {
  it("output has NO 'packageName' key", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed).not.toHaveProperty("packageName");
  });

  it("output has NO 'tsPackageName' key", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed).not.toHaveProperty("tsPackageName");
  });

  it("output has NO 'nugetId' key", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed).not.toHaveProperty("nugetId");
  });

  it("output has NO 'package' key", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed).not.toHaveProperty("package");
  });
});

// ---------------------------------------------------------------------------
// Beta channel variant
// ---------------------------------------------------------------------------

describe("emitWireIdentityManifest_BetaChannel_RecordsBetaFacts", () => {
  it("records stability 'beta' for a beta channel", () => {
    const file = emitWireIdentityManifest(
      BETA_PACKAGE,
      "D2.Services.Protos.Geo.V2Beta",
      BETA_CHANNEL,
    );
    const parsed = JSON.parse(file.content) as Record<string, unknown>;
    expect(parsed["stability"]).toBe("beta");
    expect(parsed["channel"]).toBe("v2beta");
  });
});

// ---------------------------------------------------------------------------
// File name
// ---------------------------------------------------------------------------

describe("emitWireIdentityManifest_FileName_IsWireIdentityManifestGJson", () => {
  it("fileName is wire-identity.manifest.g.json", () => {
    const file = emitWireIdentityManifest(
      PROTO_PACKAGE,
      PROTO_CSHARP_NS,
      ALPHA_CHANNEL,
    );
    expect(file.fileName).toBe("wire-identity.manifest.g.json");
  });
});
