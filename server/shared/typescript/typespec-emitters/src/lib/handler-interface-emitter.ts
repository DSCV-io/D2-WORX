// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Handler-interface emitter — pure string-template emission of the generated
// I<Op>Handler interface for one operation.
//
// The emitted interface is a one-line extends of IHandler<TInput, TOutput>.
// It lives in the per-op CQRS namespace in `app/` and is module-internal
// (never in Clients). The façade in Clients delegates through these interfaces;
// callers outside the module never import I<Op>Handler directly.
//
// Conventions (all per ADR-0020 + PATTERNS.md):
//   - `public interface I<Op>Handler : IHandler<<Op>Input, <Op>Output>;`
//   - One-line extends declaration (bare interface, no body members).
//   - Auto-generated banner, #nullable enable, namespace BEFORE using.
//   - The `using D2.Shared.Handler.Abstractions;` using is CONDITIONAL on
//     `emitUsing`:
//       false → the consuming app project supplies it via GlobalUsings.cs
//                (KC real app); no per-file using emitted.
//       true  → the fixture namespace has no global using for this assembly;
//                the per-file using is emitted.
//   - No phase/step/deliverable/audit-round identifiers in emitted code.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import { toPascal } from "./name-transforms.js";

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the generated `I<Op>Handler : IHandler<<Op>Input, <Op>Output>` interface
 * for one operation.
 *
 * Pure function — no I/O; returns an `EmittedFile` so tests can assert content directly.
 *
 * @param opName         - Operation name in lowerCamelCase (e.g. "getJwks", "sign").
 * @param namespace      - Target C# namespace for the emitted interface file.
 *                         For real KC app: `D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks`.
 *                         For fixtures: `D2.Edge.Tests.TypeSpecGrpc.Generated`.
 * @param inputTypeName  - Name of the input DTO type (e.g. "GetJwksInput").
 * @param outputTypeName - Name of the output DTO type (e.g. "GetJwksOutput").
 * @param emitUsing      - Whether to emit `using D2.Shared.Handler.Abstractions;`
 *                         (true for fixture namespaces; false when the app GlobalUsings
 *                         already supplies the import).
 * @param sourceSpec     - Relative path to the .tsp spec file (interpolated into banner).
 * @param dtoNamespace   - Optional C# namespace where the DTO types live when they are
 *                         in a different namespace from the handler interface (e.g. the
 *                         Clients project namespace for exposed ops). When provided and
 *                         different from `namespace`, a per-file `using <dtoNamespace>;`
 *                         is emitted so the handler interface can reference the DTO types.
 * @returns An `EmittedFile` whose `fileName` is `I<PascalOp>Handler.g.cs`.
 */
export function emitHandlerInterface(
  opName: string,
  namespace: string,
  inputTypeName: string,
  outputTypeName: string,
  emitUsing: boolean,
  sourceSpec: string,
  dtoNamespace?: string,
): EmittedFile {
  if (opName.length === 0)
    throw new Error("emitHandlerInterface: opName must not be empty");
  if (namespace.length === 0)
    throw new Error("emitHandlerInterface: namespace must not be empty");
  if (inputTypeName.length === 0)
    throw new Error("emitHandlerInterface: inputTypeName must not be empty");
  if (outputTypeName.length === 0)
    throw new Error("emitHandlerInterface: outputTypeName must not be empty");

  const pascalOp = toPascal(opName);
  const typeName = `I${pascalOp}Handler`;
  const banner = buildBanner(sourceSpec);

  // Emit a per-file using for the DTO namespace when the DTOs live in a different
  // namespace from the handler interface (e.g. exposed ops whose DTOs are in Clients).
  const needsDtoUsing =
    dtoNamespace !== undefined && dtoNamespace !== namespace;

  const lines: string[] = [];

  // Banner.
  lines.push(banner);

  // Nullable enable.
  lines.push("#nullable enable");
  lines.push("");

  // Namespace — always before any using directive.
  lines.push(`namespace ${namespace};`);
  lines.push("");

  // Conditional per-file using for IHandler<,>.
  if (emitUsing) {
    lines.push("using D2.Shared.Handler.Abstractions;");
    if (!needsDtoUsing) lines.push("");
  }

  // Conditional per-file using for the DTO types (when in a different namespace).
  if (needsDtoUsing) {
    lines.push(`using ${dtoNamespace};`);
    lines.push("");
  }

  // One-line extends declaration (no body — pure marker interface).
  lines.push(
    `/// <summary>Generated handler interface for the <c>${pascalOp}</c> operation.</summary>`,
  );
  lines.push(
    `public interface ${typeName} : IHandler<${inputTypeName}, ${outputTypeName}>;`,
  );
  lines.push("");

  const content = lines.join("\n");
  return { fileName: `${typeName}.g.cs`, content };
}
