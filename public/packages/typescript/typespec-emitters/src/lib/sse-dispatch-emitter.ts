// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Server-push dispatch emitter — generates the server-side DISPATCH contract
// for every @d2ServerPush op:
//
//   1. D2GeneratedSseEmitSink.g.cs — an emitter-owned faithful seam family
//      (one per registration namespace): the channel-class enum, the channel-
//      target record struct, and the generic-payload sink interface. The real
//      Edge SSE channel gateway will implement this seam; the ledger entry
//      lives in VALIDATION.md.
//
//   2. I<Op>Dispatcher.g.cs + <Op>Dispatcher.g.cs — a per-op dispatcher
//      interface + sealed impl. The channel CLASS is BAKED into the impl from
//      the op's pushTarget; the event-type is the op-name literal; the payload
//      type is the op's <Op>Output DTO (walked via the shared model walker, so
//      it inherits the temporal + enum + nested support already shipped).
//
//   3. <Module>SseDispatchersGenerated.g.cs — a per-module DI extension
//      registering each dispatcher Transient (matches the handler/facade
//      lifetime; the sink is the injected collaborator). Called from the hand-
//      written app composition root (regen-safe — the .g.cs is overwritten, the
//      manual root is never edited).
//
// The text/event-stream wire framing (data:/event: lines, serialization) stays
// hand-written fringe in the real Edge channel gateway — NOT emitted here. The
// generated dispatcher delegates a TYPED payload to the sink; the sink owns the
// wire binding. This keeps dispatch host-independent + standalone-validatable.
//
// Conventions:
//   - Generated C# follows all project conventions: banner, #nullable enable,
//     namespace before using, sealed impl, C# 14 extension block form, no this.,
//     American English, XML docs, D2Result return contract (the sink failure is
//     PROPAGATED, never swallowed to Ok).
//   - Primary-ctor param `sink` carries NO r_ prefix (handler/facade convention).
//   - The event-type is emitted as the op-name STRING LITERAL — it is a wire
//     identifier (the op name), not a translatable message, so the TK-constant
//     rule does not apply (there is no TK key for an event-type).
//   - No phase/step/deliverable/audit-round identifiers anywhere.
//   - The seam names use the D2Generated prefix to signal emitter ownership and
//     reserve a collision-free namespace vs Edge channel-gateway vocabulary
//     under replace-trigger (VALIDATION.md).

import { buildBanner } from "./banner.js";
import { toPascal } from "./name-transforms.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** The two server-push channel classes, in PascalCase C# enum-member form. */
export type SseChannelClass = "User" | "Session";

/**
 * One @d2ServerPush operation collected during the per-op navigateProgram walk.
 * The dispatch emitter receives one of these per push op and emits the per-op
 * interface + impl; the DI-ext groups all ops for a @d2ServedBy module.
 */
export interface SseDispatchOp {
  /** lowerCamelCase operation name (e.g. "orderShipped"); also the event-type literal. */
  readonly opName: string;
  /**
   * The channel class baked into the dispatcher impl, derived from the op's
   * `pushTarget` ("user" → "User", "session" → "Session").
   */
  readonly channelClass: SseChannelClass;
  /** Name of the output DTO type carried as the dispatch payload (e.g. "OrderShippedOutput"). */
  readonly outputTypeName: string;
  /**
   * C# namespace where `<Op>Output` lives (the DTO namespace). When it differs
   * from the dispatcher namespace, a per-file using is emitted so the bare
   * payload type name resolves.
   */
  readonly dtoNamespace: string;
  /** Source spec path for the banner. */
  readonly sourceSpec: string;
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the `D2GeneratedSseEmitSink.g.cs` seam family: the channel-class enum,
 * the channel-target record struct, and the generic-payload sink interface.
 *
 * The sink is generic over the payload type so each dispatcher delivers the
 * typed `<Op>Output` without boxing. Serialization + `data:`/`event:` framing is
 * the SINK's job (the hand-written fringe binding), keeping dispatch typed +
 * host-independent. The real Edge channel gateway will implement this interface;
 * the unbuilt consumer is ledgered in VALIDATION.md.
 *
 * Pure function — no I/O. Returns an {@link EmittedFile}.
 */
export function emitSseEmitSinkSeam(
  registrationNamespace: string,
  sourceSpec: string,
): EmittedFile {
  if (registrationNamespace.length === 0)
    throw new Error(
      "emitSseEmitSinkSeam: registrationNamespace must not be empty",
    );

  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${registrationNamespace};`);
  lines.push("");
  lines.push("using D2.Shared.Result;");
  lines.push("");
  lines.push(
    "/// <summary>The channel class a server-push event is addressed to.</summary>",
  );
  lines.push("public enum D2GeneratedSseChannelClass");
  lines.push("{");
  lines.push(
    "    /// <summary>A per-user channel (all of a user's sessions).</summary>",
  );
  lines.push("    User,");
  lines.push("");
  lines.push("    /// <summary>A single-session channel.</summary>");
  lines.push("    Session,");
  lines.push("}");
  lines.push("");
  lines.push("/// <summary>");
  lines.push(
    '/// Addresses a server-push event to one channel: the channel <see cref="Class"/>',
  );
  lines.push("/// (user vs session) plus the recipient identifier.");
  lines.push("/// </summary>");
  lines.push("public readonly record struct D2GeneratedSseChannelTarget(");
  lines.push("    D2GeneratedSseChannelClass Class, string Id);");
  lines.push("");
  lines.push("/// <summary>");
  lines.push("/// Faithful seam for the generated server-push dispatchers.");
  lines.push(
    "/// A generated <c>&lt;Op&gt;Dispatcher</c> delegates a TYPED payload to this sink;",
  );
  lines.push(
    "/// serialization and the <c>text/event-stream</c> framing are the sink's job.",
  );
  lines.push(
    "/// The real Edge SSE channel gateway will implement this interface.",
  );
  lines.push("/// No wire-framing logic is present in this seam definition.");
  lines.push("/// </summary>");
  lines.push("public interface D2GeneratedSseEmitSink");
  lines.push("{");
  lines.push("    /// <summary>");
  lines.push(
    '    /// Emit <paramref name="payload"/> to the channel addressed by',
  );
  lines.push(
    '    /// <paramref name="target"/> under the wire <paramref name="eventType"/>.',
  );
  lines.push(
    "    /// Returns <c>Ok</c> on success or a failure result when the channel is unavailable.",
  );
  lines.push("    /// </summary>");
  lines.push("    ValueTask<D2Result> EmitAsync<TPayload>(");
  lines.push(
    "        D2GeneratedSseChannelTarget target, string eventType, TPayload payload,",
  );
  lines.push("        CancellationToken ct = default);");
  lines.push("}");
  lines.push("");

  return {
    fileName: "D2GeneratedSseEmitSink.g.cs",
    content: lines.join("\n"),
  };
}

/**
 * Emit the per-op dispatcher pair (`I<Op>Dispatcher.g.cs` + `<Op>Dispatcher.g.cs`)
 * for one server-push op.
 *
 * The interface declares `DispatchAsync(string targetId, <Op>Output payload,
 * CancellationToken ct = default) -> ValueTask<D2Result>`. The impl is a thin
 * sealed forwarder: it constructs the `D2GeneratedSseChannelTarget` with the
 * BAKED-IN channel class + the runtime `targetId`, and delegates to the injected
 * sink under the op-name event-type literal. The sink's `D2Result` is returned
 * verbatim — never `Ok()` after the branching call (a sink failure rides through).
 *
 * Pure function — no I/O. Returns `[interfaceFile, implFile]`.
 */
export function emitSseDispatcher(input: SseDispatchOp): EmittedFile[] {
  if (input.opName.length === 0)
    throw new Error("emitSseDispatcher: opName must not be empty");
  if (input.outputTypeName.length === 0)
    throw new Error("emitSseDispatcher: outputTypeName must not be empty");

  const pascalOp = toPascal(input.opName);
  const interfaceName = `I${pascalOp}Dispatcher`;
  const implName = `${pascalOp}Dispatcher`;
  const banner = buildBanner(input.sourceSpec);

  return [
    emitDispatcherInterface(
      interfaceName,
      pascalOp,
      input.outputTypeName,
      input.dtoNamespace,
      banner,
    ),
    emitDispatcherImpl(
      interfaceName,
      implName,
      pascalOp,
      input.opName,
      input.channelClass,
      input.outputTypeName,
      input.dtoNamespace,
      banner,
    ),
  ];
}

/**
 * Emit the per-module DI extension (`<Module>SseDispatchersGenerated.g.cs`)
 * registering every push op's dispatcher Transient.
 *
 * Transient matches the handler/facade lifetime — the sink is the injected
 * collaborator. The C# 14 `extension(IServiceCollection)` block form is called
 * from the hand-written app composition root (regen-safe).
 *
 * Pure function — no I/O. Returns an {@link EmittedFile}; an empty `ops` list
 * produces no file (the deliberate "no push ops in module" behavior).
 */
export function emitSseDispatchersDiExtension(
  moduleName: string,
  ops: readonly SseDispatchOp[],
  namespace: string,
  sourceSpec: string,
): EmittedFile {
  if (moduleName.length === 0)
    throw new Error(
      "emitSseDispatchersDiExtension: moduleName must not be empty",
    );
  if (namespace.length === 0)
    throw new Error(
      "emitSseDispatchersDiExtension: namespace must not be empty",
    );
  if (ops.length === 0)
    throw new Error("emitSseDispatchersDiExtension: ops must not be empty");

  const extensionMethodName = `AddD2${moduleName}SseDispatchers`;
  const fileName = `${moduleName}SseDispatchersGenerated.g.cs`;
  const banner = buildBanner(sourceSpec);

  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${namespace};`);
  lines.push("");
  lines.push("using Microsoft.Extensions.DependencyInjection;");
  lines.push("");
  lines.push("/// <summary>");
  lines.push(
    `/// Generated DI extension that registers the ${moduleName} server-push dispatchers (Transient).`,
  );
  lines.push("/// Called from the hand-written app DI extension.");
  lines.push("/// </summary>");
  lines.push(
    `public static class ${moduleName}SseDispatchersGeneratedServiceCollectionExtensions`,
  );
  lines.push("{");
  lines.push("    extension(IServiceCollection services)");
  lines.push("    {");
  lines.push("        /// <summary>");
  lines.push(
    `        /// Registers each ${moduleName} server-push dispatcher (interface → impl) as Transient.`,
  );
  lines.push("        /// </summary>");
  lines.push(`        public IServiceCollection ${extensionMethodName}()`);
  lines.push("        {");
  for (const op of ops) {
    const pascalOp = toPascal(op.opName);
    lines.push(
      `            services.AddTransient<I${pascalOp}Dispatcher, ${pascalOp}Dispatcher>();`,
    );
  }

  lines.push("            return services;");
  lines.push("        }");
  lines.push("    }");
  lines.push("}");
  lines.push("");

  return { fileName, content: lines.join("\n") };
}

// ---------------------------------------------------------------------------
// Private helpers — one per generated file
// ---------------------------------------------------------------------------

function emitDispatcherInterface(
  interfaceName: string,
  pascalOp: string,
  outputTypeName: string,
  dtoNamespace: string,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${dtoNamespace};`);
  lines.push("");
  lines.push("using D2.Shared.Result;");
  lines.push("");
  lines.push(
    `/// <summary>Generated server-push dispatcher for the <c>${pascalOp}</c> operation.</summary>`,
  );
  lines.push(`public interface ${interfaceName}`);
  lines.push("{");
  lines.push("    /// <summary>");
  lines.push(
    `    /// Dispatches the <c>${pascalOp}</c> event payload to the recipient channel.`,
  );
  lines.push("    /// </summary>");
  lines.push("    ValueTask<D2Result> DispatchAsync(");
  lines.push(
    `        string targetId, ${outputTypeName} payload, CancellationToken ct = default);`,
  );
  lines.push("}");
  lines.push("");

  return { fileName: `${interfaceName}.g.cs`, content: lines.join("\n") };
}

function emitDispatcherImpl(
  interfaceName: string,
  implName: string,
  pascalOp: string,
  opName: string,
  channelClass: SseChannelClass,
  outputTypeName: string,
  dtoNamespace: string,
  banner: string,
): EmittedFile {
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${dtoNamespace};`);
  lines.push("");
  lines.push("using D2.Shared.Result;");
  lines.push("");
  lines.push("/// <summary>");
  lines.push(
    `/// Generated sealed server-push dispatcher for the <c>${pascalOp}</c> operation.`,
  );
  lines.push(
    `/// Addresses the <c>${channelClass}</c> channel and forwards the typed payload to`,
  );
  lines.push(
    "/// the injected sink under the op-name event-type. Registered Transient.",
  );
  lines.push("/// </summary>");
  lines.push(
    `public sealed class ${implName}(D2GeneratedSseEmitSink sink) : ${interfaceName}`,
  );
  lines.push("{");
  lines.push("    /// <inheritdoc/>");
  lines.push("    public ValueTask<D2Result> DispatchAsync(");
  lines.push(
    `        string targetId, ${outputTypeName} payload, CancellationToken ct = default)`,
  );
  lines.push("        => sink.EmitAsync(");
  lines.push(
    `            new D2GeneratedSseChannelTarget(D2GeneratedSseChannelClass.${channelClass}, targetId),`,
  );
  lines.push(`            "${opName}", payload, ct);`);
  lines.push("}");
  lines.push("");

  return { fileName: `${implName}.g.cs`, content: lines.join("\n") };
}
