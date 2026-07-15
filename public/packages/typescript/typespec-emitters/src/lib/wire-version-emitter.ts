// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// WireVersion constant emitter.
//
// Emits WireVersion.g.cs — a minimal C# static class that records the
// wire-generation channel as public constants. Co-located in the
// proto-csharp-namespace so a runtime confirming a compatible generation
// references e.g. D2.Services.Protos.KeyCustodian.V2Alpha.WireVersion.CHANNEL.
//
// C# side only. The contract-side artifact that a future runtime handshake
// will depend on. No handshake logic here — just the constant declaration.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import type { WireChannel } from "./wire-channel.js";

/**
 * Emit WireVersion.g.cs — the wire-generation constant in the proto C# namespace.
 *
 * @param protoCsharpNs - The `proto-csharp-namespace` tspconfig value, e.g.
 *   "D2.Services.Protos.KeyCustodian.V2Alpha". Determines the C# namespace.
 * @param channel - The parsed WireChannel triple from validateChannelAgreement.
 * @param sourceSpec - The source spec path hint for the auto-generated banner.
 * @returns An EmittedFile whose content is the complete WireVersion.g.cs source.
 */
export function emitWireVersionConstant(
  protoCsharpNs: string,
  channel: WireChannel,
  sourceSpec: string,
): EmittedFile {
  const banner = buildBanner(sourceSpec);

  const content = [
    banner,
    "#nullable enable",
    "",
    `namespace ${protoCsharpNs};`,
    "",
    "/// <summary>",
    "/// Wire-generation constants for this proto package.",
    "/// Use CHANNEL to confirm a compatible wire generation at runtime.",
    "/// </summary>",
    "public static class WireVersion",
    "{",
    `    public const string CHANNEL = "${channel.lowerChannel}";`,
    `    public const int    GENERATION = ${channel.generation.toString()};`,
    `    public const string STABILITY = "${channel.stability}";`,
    "}",
    "",
  ].join("\n");

  return { fileName: "WireVersion.g.cs", content };
}
