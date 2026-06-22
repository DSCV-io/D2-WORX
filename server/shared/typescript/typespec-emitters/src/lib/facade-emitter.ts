// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Façade emitter — pure string-template emission of the three generated
// files that form the module façade layer for one module (one module = one
// @d2ServedBy value):
//
//   1. I<Module>Api.g.cs  → Clients project
//      The curated public interface listing ONLY the exposed operations.
//      Internal-only (@d2Internal) ops are structurally absent — the
//      structural-absence property prevents callers from accidentally calling
//      an op that was never meant to cross a boundary.
//
//   2. <Module>Api.g.cs   → app/ project
//      The thin delegating implementation. One primary-constructor parameter
//      per exposed op (I<Op>Handler). Each method delegates to the matching
//      handler's HandleAsync call.
//
//   3. <Module>ClientsGenerated.g.cs  → app/ project
//      The generated DI extension that registers the façade impl as Transient.
//      Lifetime is Transient (not Singleton) to match the handler lifetime —
//      the impl injects transient handlers that depend on the scoped DbContext;
//      a Singleton façade would capture the scoped DbContext (captive-dependency).
//
// Signature shape (transport-neutral):
//   ValueTask<D2Result<<Op>Output?>> <Op>Async(<Op>Input input, CancellationToken ct = default)
//
// No HandlerOptions? parameter — the SAME interface must back both the
// in-process impl (today) and a future gRPC-client impl; HandlerOptions is
// a server-side concern that cannot be expressed on a wire boundary.
//
// Conventions:
//   - Auto-generated banner, #nullable enable, namespace BEFORE using.
//   - C# 14 extension(IServiceCollection) block form for the DI extension.
//   - sealed class for the impl; public interface for the façade interface.
//   - No phase/step/deliverable/audit-round identifiers in emitted code.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";
import { toPascal } from "./name-transforms.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/**
 * One exposed operation, collected during the per-op navigateProgram walk.
 * The façade emitter receives a list of these for each module (grouped by
 * @d2ServedBy value) and emits three files.
 */
export interface ExposedOp {
  /** lowerCamelCase operation name (e.g. "getJwks"). */
  readonly opName: string;
  /** Name of the input DTO type (e.g. "GetJwksInput"). */
  readonly inputTypeName: string;
  /** Name of the output DTO type (e.g. "GetJwksOutput"). */
  readonly outputTypeName: string;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
  /**
   * CQRS category: "Commands" or "Queries".
   * Used to compute the handler-interface namespace for the impl using directive.
   */
  readonly category: "Commands" | "Queries";
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the three façade-layer files for one module.
 *
 * Pure function — no I/O. Returns the three `EmittedFile` instances in order:
 *   [0] the interface file (targets the Clients project namespace),
 *   [1] the impl file (targets the app/ project namespace),
 *   [2] the DI extension file (targets the app/ project namespace).
 *
 * @param moduleName       - The @d2ServedBy module name in PascalCase
 *                           (e.g. "KeyCustodian"). Drives the interface/impl/DI
 *                           type names and file names.
 * @param exposedOps       - All exposed operations for this module, in the order
 *                           they were encountered during the program walk (the
 *                           order determines the method order in the interface
 *                           and the constructor-parameter order in the impl).
 *                           Must not be empty (a zero-op module produces no file).
 * @param clientsNamespace - The C# namespace for the Clients project (where the
 *                           interface and DTO types live). Used for both the
 *                           namespace declaration of the interface file AND the
 *                           `using` directive in the app-ns files.
 * @param appNamespace     - The C# namespace for the generated app-layer files
 *                           (impl + DI extension). Typically the module's
 *                           Application namespace root (e.g.
 *                           "D2.Edge.KeyCustodian.App.Application").
 * @returns An array of exactly three EmittedFile instances, or an empty array
 *          when `exposedOps` is empty (zero-exposed-op module → no façade).
 */
export function emitFacade(
  moduleName: string,
  exposedOps: readonly ExposedOp[],
  clientsNamespace: string,
  appNamespace: string,
): EmittedFile[] {
  if (moduleName.length === 0)
    throw new Error("emitFacade: moduleName must not be empty");
  if (clientsNamespace.length === 0)
    throw new Error("emitFacade: clientsNamespace must not be empty");
  if (appNamespace.length === 0)
    throw new Error("emitFacade: appNamespace must not be empty");

  // Zero-exposed-op module → no façade interface (the deliberate "empty module" behavior).
  if (exposedOps.length === 0) return [];

  const interfaceName = `I${moduleName}Api`;
  const implName = `${moduleName}Api`;

  // Use the first op's sourceSpec for the banner (all ops in a module share the same spec).
  const sourceSpec = exposedOps[0]!.sourceSpec;
  const banner = buildBanner(sourceSpec);

  return [
    emitInterface(interfaceName, exposedOps, clientsNamespace, banner),
    emitImpl(
      interfaceName,
      implName,
      exposedOps,
      clientsNamespace,
      appNamespace,
      banner,
    ),
    emitDiExtension(
      interfaceName,
      implName,
      moduleName,
      clientsNamespace,
      appNamespace,
      banner,
    ),
  ];
}

// ---------------------------------------------------------------------------
// Private helpers — one per generated file
// ---------------------------------------------------------------------------

function emitInterface(
  interfaceName: string,
  exposedOps: readonly ExposedOp[],
  clientsNamespace: string,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  lines.push(banner);
  lines.push("#nullable enable");
  lines.push("");
  lines.push(`namespace ${clientsNamespace};`);
  lines.push("");

  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated internal API façade for the module. Lists only the operations`,
  );
  lines.push(
    `/// exposed across a boundary; internal-only operations are absent.`,
  );
  lines.push(`/// </summary>`);
  lines.push(`public interface ${interfaceName}`);
  lines.push("{");

  for (const op of exposedOps) {
    const pascalOp = toPascal(op.opName);
    lines.push(
      `    /// <summary>Dispatches the <c>${pascalOp}</c> operation.</summary>`,
    );
    lines.push(
      `    ValueTask<D2Result<${op.outputTypeName}?>> ${pascalOp}Async(` +
        `${op.inputTypeName} input, CancellationToken ct = default);`,
    );
  }

  lines.push("}");
  lines.push("");

  return { fileName: `${interfaceName}.g.cs`, content: lines.join("\n") };
}

function emitImpl(
  interfaceName: string,
  implName: string,
  exposedOps: readonly ExposedOp[],
  clientsNamespace: string,
  appNamespace: string,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  // Compute all using namespaces and sort alphabetically (SA1210 compliance).
  // Handler namespaces: e.g. D2.Edge.KeyCustodian.App.Application.Handlers.Queries.GetJwks
  const allUsings = [
    clientsNamespace,
    ...exposedOps.map(
      (op) => `${appNamespace}.Handlers.${op.category}.${toPascal(op.opName)}`,
    ),
  ].sort();

  lines.push(banner);
  lines.push("#nullable enable");
  lines.push("");
  lines.push(`namespace ${appNamespace};`);
  lines.push("");
  for (const ns of allUsings) lines.push(`using ${ns};`);
  lines.push("");

  // Build the primary constructor parameter list.
  const ctorParams = exposedOps
    .map((op) => {
      const pascalOp = toPascal(op.opName);
      const paramName = `${op.opName}Handler`;
      return `I${pascalOp}Handler ${paramName}`;
    })
    .join(",\n    ");

  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated façade implementation. Delegates each exposed operation to the`,
  );
  lines.push(
    `/// corresponding app-layer handler. Registered Transient to match handler lifetime.`,
  );
  lines.push(`/// </summary>`);
  lines.push(`public sealed class ${implName}(`);
  lines.push(`    ${ctorParams}) : ${interfaceName}`);
  lines.push("{");

  for (const op of exposedOps) {
    const pascalOp = toPascal(op.opName);
    const paramName = `${op.opName}Handler`;
    lines.push(`    /// <inheritdoc/>`);
    lines.push(
      `    public ValueTask<D2Result<${op.outputTypeName}?>> ${pascalOp}Async(` +
        `${op.inputTypeName} input, CancellationToken ct = default)`,
    );
    lines.push(`        => ${paramName}.HandleAsync(input, ct);`);
  }

  lines.push("}");
  lines.push("");

  return { fileName: `${implName}.g.cs`, content: lines.join("\n") };
}

function emitDiExtension(
  interfaceName: string,
  implName: string,
  moduleName: string,
  clientsNamespace: string,
  appNamespace: string,
  banner: string,
): EmittedFile {
  const extensionMethodName = `AddD2${moduleName}Clients`;
  const fileName = `${moduleName}ClientsGenerated.g.cs`;

  const lines: string[] = [];

  lines.push(banner);
  lines.push("#nullable enable");
  lines.push("");
  lines.push(`namespace ${appNamespace};`);
  lines.push("");
  // The interface lives in the Clients namespace — a using is required.
  lines.push(`using ${clientsNamespace};`);
  lines.push("");

  lines.push(`/// <summary>`);
  lines.push(
    `/// Generated DI extension that registers the module façade (Transient).`,
  );
  lines.push(`/// Called from the hand-written app DI extension.`);
  lines.push(`/// </summary>`);
  lines.push(
    `public static class ${moduleName}ClientsGeneratedServiceCollectionExtensions`,
  );
  lines.push("{");
  lines.push(`    extension(IServiceCollection services)`);
  lines.push("    {");
  lines.push(`        /// <summary>`);
  lines.push(
    `        /// Registers <see cref="${interfaceName}"/> → <see cref="${implName}"/> as Transient.`,
  );
  lines.push(`        /// </summary>`);
  lines.push(`        public IServiceCollection ${extensionMethodName}()`);
  lines.push("        {");
  lines.push(
    `            services.AddTransient<${interfaceName}, ${implName}>();`,
  );
  lines.push(`            return services;`);
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return { fileName, content: lines.join("\n") };
}
