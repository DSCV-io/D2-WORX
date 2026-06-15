// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { navigateProgram } from "@typespec/compiler";
import type { EmitContext } from "@typespec/compiler";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_IN_PROCESS_KEY,
} from "@d2/typespec-decorators";
import { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";

// Smoke emit — proves the full pipeline: tsp compile → $onEmit → emitFile.
//
// Discovers all operations in the compiled program and reads back the three
// decorator state keys shipped by @d2/typespec-decorators (@d2ServedBy,
// @d2GrpcMethod, @d2InProcess). Emits an operations-manifest.json as proof.
// No banner (JSON has no comment syntax). The real emitters in this fleet
// follow the same navigateProgram → stateMap read-back → emitFile pattern.

/** Shape of one operation entry in the smoke manifest. */
export interface ManifestOperation {
  readonly name: string;
  readonly servedBy: string | undefined;
  readonly hasGrpc: boolean;
  readonly inProcess: boolean;
}

/** Shape of the emitted operations-manifest.json. */
export interface OperationsManifest {
  readonly emitter: string;
  readonly operationCount: number;
  readonly operations: readonly ManifestOperation[];
}

/**
 * TypeSpec emitter entry point. Called once per tsp compile when this package
 * appears in the consumer's tspconfig.yaml `emit:` list.
 */
export async function $onEmit(context: EmitContext): Promise<void> {
  const program = context.program;
  const ops: ManifestOperation[] = [];

  navigateProgram(program, {
    operation(op) {
      ops.push({
        name: op.name,
        servedBy: program.stateMap(D2_SERVED_BY_KEY).get(op) as
          | string
          | undefined,
        hasGrpc: program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined,
        inProcess:
          program.stateMap(D2_IN_PROCESS_KEY).get(op) === true,
      });
    },
  });

  const manifest: OperationsManifest = {
    emitter: "@d2/typespec-emitters",
    operationCount: ops.length,
    operations: ops,
  };

  const path = resolveOutputPath(context, "operations-manifest.json");
  await emitGeneratedFile(program, path, JSON.stringify(manifest, null, 2));
}
