// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Package barrel — the API surface that consumers import.
// Re-exports the emitter entry point, the $lib descriptor, and the
// shared-lib public surface (scalar registry, name transforms, banner,
// emit-file utilities) so downstream steps import from the barrel.

export { $onEmit } from "./emitter.js";
export type { ManifestOperation, OperationsManifest } from "./emitter.js";

export { $lib } from "./lib.js";

export { resolveScalar, hasScalar } from "./lib/scalar-registry.js";
export type { ScalarMapping } from "./lib/scalar-registry.js";

export { toSnake, toPascal } from "./lib/name-transforms.js";

export { buildBanner } from "./lib/banner.js";

export { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";
