// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Wire-identity manifest emitter.
//
// Emits wire-identity.manifest.g.json — a minimal JSON document recording
// the agree-by-construction wire-identity facts: proto package, proto C#
// namespace, generation, stability, and channel. Deliberately omits any
// published npm/NuGet package name — the package-name-with-channel
// convention is unresolved (tracked for a later packaging step).
//
// JSON output: no "//" banner (mirrors openapi-emitter.ts:30-34 + the smoke
// manifest). Provenance recorded in-document via x-d2-generated-by.

import type { EmittedFile } from "./csharp-dto-emitter.js";
import type { WireChannel } from "./wire-channel.js";

/** Shape of the emitted wire-identity manifest. */
export interface WireIdentityManifest {
  readonly protoPackage: string;
  readonly protoCsharpNamespace: string;
  readonly generation: number;
  readonly stability: "alpha" | "beta" | "stable";
  readonly channel: string;
  readonly "x-d2-generated-by": string;
}

/**
 * Emit wire-identity.manifest.g.json — the wire-identity facts record.
 *
 * @param protoPackage - The full proto-package string, e.g. "d2.keycustodian.v2alpha".
 * @param protoCsharpNs - The proto C# namespace, e.g. "D2.Services.Protos.KeyCustodian.V2Alpha".
 * @param channel - The parsed WireChannel triple from validateChannelAgreement.
 * @returns An EmittedFile whose content is the complete manifest JSON.
 */
export function emitWireIdentityManifest(
  protoPackage: string,
  protoCsharpNs: string,
  channel: WireChannel,
): EmittedFile {
  const manifest: WireIdentityManifest = {
    protoPackage,
    protoCsharpNamespace: protoCsharpNs,
    generation: channel.generation,
    stability: channel.stability,
    channel: channel.lowerChannel,
    "x-d2-generated-by": "@dcsv-io/d2-typespec-emitters",
  };

  return {
    fileName: "wire-identity.manifest.g.json",
    content: JSON.stringify(manifest, null, 2) + "\n",
  };
}
