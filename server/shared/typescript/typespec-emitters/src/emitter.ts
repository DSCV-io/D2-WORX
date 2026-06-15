// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { navigateProgram, NoTarget } from "@typespec/compiler";
import type { EmitContext, Model, Operation } from "@typespec/compiler";
import {
  D2_SERVED_BY_KEY,
  D2_GRPC_METHOD_KEY,
  D2_IN_PROCESS_KEY,
} from "@d2/typespec-decorators";
import { emitGeneratedFile, resolveOutputPath } from "./lib/emit-file.js";
import { walkModel } from "./lib/model-walk.js";
import { emitCsharpDtos } from "./lib/csharp-dto-emitter.js";
import { emitTsDtos } from "./lib/ts-dto-emitter.js";
import { $lib } from "./lib.js";

// The $onEmit entry point drives three artifacts per tsp compile:
//
//   1. operations-manifest.json — operations smoke manifest from the initial
//      scaffold; kept so the operations-manifest integration test stays green
//      alongside DTO emission.
//   2. <Op>Input.g.cs + <Op>Output.g.cs — C# sealed-record DTO pairs for
//      every operation with a concrete input or output model.
//   3. <op>-dto.g.ts — TypeScript interface pair for the same operations.
//
// Namespace for C# is read from the tspconfig emitter option `csharp-namespace`.
// Each operation's Input/Output pair lands under that namespace. When the
// option is absent, a safe default is used and a warning is noted via the
// emitter package name in the banner.

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

  // Read the csharp-namespace emitter option; fall back to a safe placeholder.
  const rawOptions = (context.options ?? {}) as Record<string, unknown>;
  const csNamespace =
    typeof rawOptions["csharp-namespace"] === "string" && rawOptions["csharp-namespace"].length > 0
      ? rawOptions["csharp-namespace"]
      : "D2.Generated";

  navigateProgram(program, {
    operation(op: Operation) {
      ops.push({
        name: op.name,
        servedBy: program.stateMap(D2_SERVED_BY_KEY).get(op) as string | undefined,
        hasGrpc: program.stateMap(D2_GRPC_METHOD_KEY).get(op) !== undefined,
        inProcess: program.stateMap(D2_IN_PROCESS_KEY).get(op) === true,
      });

      // Derive the source spec path hint for the banner.
      // TypeSpec exposes the file via op.file / op.node?.file — use a relative
      // path if available; fall back to the operation name as a hint.
      const specHint = tryGetSpecPath(op) ?? `<typespec op: ${op.name}>`;

      // Resolve input model: TypeSpec op parameters is a Model whose properties
      // are the named params. When the op has no params the properties map is empty.
      //
      // When the op has a single named param (e.g. `op sign(input: SignInput)`),
      // the parameters model has one property `input` whose type is `SignInput`.
      // We unwrap that to use the model directly so field-level @d2Redact state
      // (stored on the inner model's properties) is accessible.
      const rawParams = op.parameters as Model | undefined;
      const inputModel = rawParams !== undefined
        ? (resolveSingleNamedParam(rawParams) ?? rawParams)
        : undefined;
      const outputModel = op.returnType?.kind === "Model" ? (op.returnType as Model) : undefined;

      // Emit C# + TS DTOs only when we have at least one side with a concrete model.
      if (inputModel !== undefined || outputModel !== undefined) {
        emitDtoPair(context, program, op.name, csNamespace, specHint, inputModel, outputModel);
      }
    },
  });

  // ---- Smoke manifest (kept so the operations-manifest integration test stays green) --------
  const manifest: OperationsManifest = {
    emitter: "@d2/typespec-emitters",
    operationCount: ops.length,
    operations: ops,
  };

  const manifestPath = resolveOutputPath(context, "operations-manifest.json");
  await emitGeneratedFile(program, manifestPath, JSON.stringify(manifest, null, 2));
}

// ---------------------------------------------------------------------------
// Internal helpers
// ---------------------------------------------------------------------------

function emitDtoPair(
  context: EmitContext,
  program: Parameters<typeof walkModel>[0],
  opName: string,
  csNamespace: string,
  specHint: string,
  inputModel: Model | undefined,
  outputModel: Model | undefined,
): void {
  const errors: string[] = [];
  const onError = (
    code: "unmapped-scalar" | "unsupported-property-type",
    message: string,
  ): void => {
    errors.push(message);
    // Report a TypeSpec diagnostic so tsp compile exits non-zero (error severity).
    // The `message` string already contains the full human-readable description;
    // we pass it through the format parameter that matches the paramMessage template.
    if (code === "unmapped-scalar")
      $lib.reportDiagnostic(program, { code: "unmapped-scalar", format: { scalar: message }, target: NoTarget });
    else
      // unsupported-property-type: split the message to extract kind + property context.
      $lib.reportDiagnostic(program, {
        code: "unsupported-property-type",
        format: { kind: "unsupported", property: message },
        target: NoTarget,
      });
  };

  // Walk input model (empty walk when op has no params).
  const inputWalk = inputModel !== undefined
    ? walkModel(program, inputModel, onError)
    : { fields: [], nestedModels: [] };

  // Walk output model (empty when op returns void).
  const outputWalk = outputModel !== undefined
    ? walkModel(program, outputModel, onError)
    : { fields: [], nestedModels: [] };

  if (errors.length > 0)
    return; // Diagnostics already reported; don't emit partial files.

  // ---- C# DTO emission ----
  const csFiles = emitCsharpDtos(
    opName,
    csNamespace,
    specHint,
    inputWalk.fields,
    outputWalk.fields,
    outputWalk.nestedModels,
  );

  for (const f of csFiles) {
    const path = resolveOutputPath(context, f.fileName);
    void emitGeneratedFile(program, path, f.content);
  }

  // ---- TS DTO emission ----
  const tsFile = emitTsDtos(
    opName,
    specHint,
    inputWalk.fields,
    outputWalk.fields,
    outputWalk.nestedModels,
  );

  const tsPath = resolveOutputPath(context, tsFile.fileName);
  void emitGeneratedFile(program, tsPath, tsFile.content);
}

/**
 * Attempt to extract a human-readable source-spec path from the operation node.
 * Returns undefined when the TypeSpec version does not expose the file path.
 */
function tryGetSpecPath(op: Operation): string | undefined {
  const node = op.node as { file?: { path?: string } } | undefined;
  return node?.file?.path;
}

/**
 * When an op has exactly one named parameter whose type is a Model (e.g.
 * `op sign(input: SignInput): SignOutput`), the TypeSpec parameters model
 * wraps that single model in an anonymous container. Unwrap it so the emitter
 * walks the inner model's properties (where @d2Redact state is stored).
 *
 * Returns undefined for flat-params ops (getJwks-style: no params or directly
 * flat scalar params on the op), in which case the caller uses the raw
 * parameters model as-is.
 */
function resolveSingleNamedParam(params: Model): Model | undefined {
  if (params.properties === undefined || params.properties.size !== 1) return undefined;
  const [, prop] = [...params.properties.entries()][0]!;
  if (prop.type.kind === "Model" && prop.type.name !== "Array") return prop.type;
  return undefined;
}
