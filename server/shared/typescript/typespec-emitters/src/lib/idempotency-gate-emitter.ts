// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Idempotency-gate emitter — generates:
//
//   1. D2GeneratedIdempotencyStore.g.cs — an emitter-owned faithful seam
//      interface (one per registration namespace) that the generated gate
//      depends on. The real Edge HTTP-idempotency middleware will implement
//      this seam; the ledger entry lives in VALIDATION.md.
//
//   2. A set of C# statement fragments (param, pre-delegate lines, post-
//      delegate lines, extra usings) that `route-policy-emitter.ts` weaves
//      into the generated route delegate body AFTER auth enforcement and
//      BEFORE / AFTER the façade/handler delegation call.
//
// Conventions:
//   - Generated C# follows all project conventions: banner, #nullable enable,
//     namespace before using, sealed, C# 14 extension block form, no this.,
//     American English, XML docs, sorted usings, Falsey() for null/empty/ws,
//     D2Result semantic factories, string.Empty, brace-free one-liner ifs.
//   - No phase/step/deliverable/audit-round identifiers anywhere.
//   - The seam name uses the D2Generated prefix to signal emitter ownership
//     and to avoid colliding with the future real Edge IIdempotencyStore.

import { buildBanner } from "./banner.js";
import type { EmittedFile } from "./csharp-dto-emitter.js";

// ---------------------------------------------------------------------------
// Public types
// ---------------------------------------------------------------------------

/** keySource variants supported by the gate emitter. */
export type IdempotencyKeySource = "header" | "derived";

/**
 * Everything the gate emitter needs to weave the idempotency gate into one
 * route delegate.
 *
 * `fields` is a list of PascalCase C# property names to hash for the derived
 * key (the emitter caller maps from lowerCamel `.tsp` field names to C#
 * PascalCase before passing them here).
 */
export interface IdempotencyGateInput {
  /** The key-extraction strategy. */
  readonly keySource: IdempotencyKeySource;
  /** Replay window in seconds; emitted as `TimeSpan.FromSeconds(<ttlSeconds>)`. */
  readonly ttlSeconds: number;
  /**
   * PascalCase property names of the input DTO to hash (derived key only).
   * Empty for header keySource (guaranteed by the decorator-layer validator).
   */
  readonly fields: readonly string[];
  /** C# input DTO type name (e.g. "SignInput") — used for derived key field access. */
  readonly inputTypeName: string;
  /** C# output DTO type name (e.g. "SignOutput") — used as the store's TStored type. */
  readonly outputTypeName: string;
  /** The op name in PascalCase (e.g. "Sign") — used for the store variable name. */
  readonly pascalOpName: string;
}

/**
 * The weave fragments produced by {@link buildIdempotencyGate}.
 * `route-policy-emitter.ts` splices these into the generated delegate.
 */
export interface IdempotencyGateWeave {
  /**
   * Additional using namespaces required by the gate (to be merged with the
   * route file's existing usings and re-sorted).
   */
  readonly extraUsings: readonly string[];
  /** The `D2GeneratedIdempotencyStore store` DI parameter literal. */
  readonly storeParam: string;
  /**
   * Lines to emit BEFORE the delegation call (key resolution + replay check).
   * Each line is indented to the delegate body level (20 spaces = 5 levels).
   */
  readonly preDelegateLines: readonly string[];
  /**
   * Lines to emit AFTER the delegation call (store the outcome with TTL).
   * Each line is indented to the delegate body level (20 spaces = 5 levels).
   */
  readonly postDelegateLines: readonly string[];
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Emit the `D2GeneratedIdempotencyStore.g.cs` file: a small emitter-owned
 * faithful seam interface for result-replay idempotency.
 *
 * The interface is generic over the stored value type so each route can
 * replay the typed `<Op>Output?` result without boxing. The two methods
 * form the core replay contract:
 *   - `TryGetAsync<TStored>` — `Ok(stored)` on hit, `NotFound` on miss,
 *     failure on store error (gate fails-open on read failure).
 *   - `StoreAsync<TStored>` — store the outcome with TTL, return `Ok` or
 *     failure (failure is logged by the middleware; gate proceeds on write failure).
 *
 * This seam is distinct from `IMessageIdempotencyStore` (the messaging
 * boolean dedup store). The real Edge HTTP-idempotency middleware will
 * implement this interface; the unbuilt consumer is ledgered in VALIDATION.md.
 *
 * Pure function — no I/O. Returns an {@link EmittedFile}.
 */
export function emitIdempotencyStoreSeam(
  registrationNamespace: string,
  sourceSpec: string,
): EmittedFile {
  if (registrationNamespace.length === 0)
    throw new Error(
      "emitIdempotencyStoreSeam: registrationNamespace must not be empty",
    );

  const banner = buildBanner(sourceSpec);
  const lines: string[] = [];

  lines.push(banner + "#nullable enable");
  lines.push("");
  lines.push(`namespace ${registrationNamespace};`);
  lines.push("");
  lines.push("using D2.Shared.Result;");
  lines.push("");
  lines.push("/// <summary>");
  lines.push("/// Faithful seam for the generated idempotency gate.");
  lines.push(
    "/// Supports result-replay: on a cache hit the stored <c>D2Result</c> is",
  );
  lines.push(
    "/// returned verbatim without re-invoking the handler or façade.",
  );
  lines.push(
    "/// The real Edge HTTP-idempotency middleware will implement this interface.",
  );
  lines.push("/// No enforcement logic is present in this seam definition.");
  lines.push("/// </summary>");
  lines.push("public interface D2GeneratedIdempotencyStore");
  lines.push("{");
  lines.push("    /// <summary>");
  lines.push(
    '    /// Try to retrieve a previously stored result for <paramref name="key"/>.',
  );
  lines.push(
    "    /// Returns <c>Ok(stored)</c> on a hit, <c>NotFound</c> on a miss,",
  );
  lines.push("    /// or a failure result when the store is unavailable.");
  lines.push("    /// </summary>");
  lines.push(
    "    ValueTask<D2Result<TStored?>> TryGetAsync<TStored>(string key, CancellationToken ct = default);",
  );
  lines.push("");
  lines.push("    /// <summary>");
  lines.push(
    '    /// Store <paramref name="value"/> under <paramref name="key"/> with the',
  );
  lines.push(
    '    /// given <paramref name="ttl"/>. The entry expires after the TTL elapses.',
  );
  lines.push(
    "    /// Returns <c>Ok</c> on success or a failure result when the store is unavailable.",
  );
  lines.push("    /// </summary>");
  lines.push(
    "    ValueTask<D2Result> StoreAsync<TStored>(string key, TStored value, TimeSpan ttl, CancellationToken ct = default);",
  );
  lines.push("}");
  lines.push("");

  return {
    fileName: "D2GeneratedIdempotencyStore.g.cs",
    content: lines.join("\n"),
  };
}

/**
 * Build the C# statement fragments that implement the idempotency gate
 * inside a generated route delegate.
 *
 * The gate enforces the following contract:
 *   1. Resolve the idempotency key (from the `Idempotency-Key` header for
 *      `header` keySource, or from a SHA-256 hash of named input fields for
 *      `derived` keySource).
 *   2. For `header` keySource: if the header is absent or whitespace, return
 *      `D2Result.ValidationFailed` immediately (400 via MAP-ii). `Falsey()`
 *      covers null / empty / whitespace in one call.
 *   3. Call `store.TryGetAsync(key)` — on a hit, replay the stored result
 *      verbatim without invoking the delegation target. On a store-read
 *      outage (non-NotFound failure), fail-open and proceed to delegate.
 *   4. After delegation, call `store.StoreAsync(key, result, ttl)` — the
 *      outcome (success or failure) is always stored so retries replay the
 *      same result.
 *
 * Gate position in the delegate body:
 *   - `preDelegateLines` are emitted BEFORE the delegation call (at the top
 *     of the async lambda body, after auth runs but before handler/façade).
 *   - `postDelegateLines` are emitted AFTER the delegation call but BEFORE
 *     the MAP-ii success check — caller must restructure to `result` being
 *     the final value.
 *
 * Throws loudly when:
 *   - `keySource` is not `"header"` or `"derived"` (unknown strategy).
 *   - `fields` is empty for `derived` keySource (decorator invariant; fail-loud).
 *   - Any field name in `fields` is empty.
 *
 * Pure function — no I/O.
 */
export function buildIdempotencyGate(
  input: IdempotencyGateInput,
): IdempotencyGateWeave {
  if (input.keySource !== "header" && input.keySource !== "derived")
    throw new Error(
      `buildIdempotencyGate: unknown keySource '${input.keySource}' — expected 'header' or 'derived'`,
    );
  if (input.keySource === "derived") {
    if (input.fields.length === 0)
      throw new Error(
        "buildIdempotencyGate: derived keySource requires at least one field name",
      );
    for (const f of input.fields) {
      if (f.length === 0)
        throw new Error("buildIdempotencyGate: field name must not be empty");
    }
  }

  const ind = "                    "; // 20 spaces — delegate body indent
  const preDelegateLines: string[] = [];

  if (input.keySource === "header") {
    // Resolve the idempotency key from the Idempotency-Key header.
    // Use the literal "Idempotency-Key" — no D2.Shared.Headers.Http project
    // reference in the generated fixture's host project; constant follow-up tracked.
    preDelegateLines.push(
      `${ind}var idempotencyKey = http.Request.Headers["Idempotency-Key"].ToString();`,
    );
    preDelegateLines.push(`${ind}if (idempotencyKey.Falsey())`);
    preDelegateLines.push(`${ind}    return Results.Json(`);
    preDelegateLines.push(
      `${ind}        D2Result<${input.outputTypeName}?>.ValidationFailed().ToProblemDetails(http),`,
    );
    preDelegateLines.push(`${ind}        statusCode: 400,`);
    preDelegateLines.push(
      `${ind}        contentType: "application/problem+json");`,
    );
  } else {
    // Derived key: SHA-256 over the named PascalCase input fields, separator-joined.
    // SHA256.HashData + Convert.ToHexStringLower — BCL statics, no allocation.
    const fieldAccesses = input.fields
      .map((f) => `input.${f}`)
      .join(' + "|" + ');
    preDelegateLines.push(
      `${ind}var idempotencyKeyRaw = System.Text.Encoding.UTF8.GetBytes(${fieldAccesses});`,
    );
    preDelegateLines.push(
      `${ind}var idempotencyKey = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(idempotencyKeyRaw));`,
    );
  }

  // Cache-hit check: replay the stored D2Result<TOut> without re-invoking the delegate.
  // TStored = D2Result<TOut>; the outer D2Result wraps the store look-up status.
  // Fail-open on store-read outage (non-Success, non-NotFound) — proceed to delegate.
  const storeType = `D2Result<${input.outputTypeName}?>`;
  preDelegateLines.push(
    `${ind}var cachedResult = await store.TryGetAsync<${storeType}>(idempotencyKey, ct).ConfigureAwait(false);`,
  );
  preDelegateLines.push(
    `${ind}if (cachedResult.Success && cachedResult.Data is not null)`,
  );
  preDelegateLines.push(`${ind}{`);
  preDelegateLines.push(`${ind}    var replayed = cachedResult.Data;`);
  preDelegateLines.push(
    `${ind}    var replayStatus = (int)replayed.StatusCode;`,
  );
  preDelegateLines.push(`${ind}    if (replayStatus < 400)`);
  preDelegateLines.push(
    `${ind}        return Results.Json(replayed.Data, statusCode: replayStatus);`,
  );
  preDelegateLines.push(`${ind}    var rpd = replayed.ToProblemDetails(http);`);
  preDelegateLines.push(
    `${ind}    return Results.Json(rpd, statusCode: rpd.Status ?? 500, contentType: "application/problem+json");`,
  );
  preDelegateLines.push(`${ind}}`);

  // Post-delegate: store the entire D2Result<TOut> outcome (success or failure) with TTL.
  // Always store — idempotency replays the same result so retries get the original outcome.
  const ttl = `TimeSpan.FromSeconds(${input.ttlSeconds})`;
  const postDelegateLines: string[] = [
    `${ind}await store.StoreAsync<${storeType}>(idempotencyKey, result, ${ttl}, ct).ConfigureAwait(false);`,
  ];

  const extraUsings: string[] = ["D2.Shared.Utilities.Extensions"];

  return {
    extraUsings,
    storeParam: "D2GeneratedIdempotencyStore store",
    preDelegateLines,
    postDelegateLines,
  };
}
