// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// OpenAPI arm — hand-rolled diff over committed `*.openapi.g.json` documents.
//
// Detects the closed REST break-set:
//   1. Removed path (paths.<path> gone).
//   2. Removed operation (paths.<path>.<method> gone).
//   3. Response narrowing: response schema loses a required[] member or a property.
//   4. Request strengthening: request body schema gains a required[] member.
//   5. Type narrowing: a schema property's `type` changes.
//   6. Dropped enum value: a schema property's enum[] loses a member.
//   7. Removed component schema: components.schemas.<name> gone.
//
// $ref resolution: the arm resolves `$ref` values that start with
// `#/components/schemas/` against the same document's `components.schemas`.
// Dangling $refs (refs that point to a non-existent schema) produce a
// fail-loud finding rather than a silent pass.
//
// Dormancy note: today's committed *.openapi.g.json files are fixtures
// (no stable-channel REST surface exists). The arm is proven non-vacuous
// via fixture tests but is informational-only on live docs until a stable
// REST channel graduates. See VALIDATION.md.
//
// Direction note (request vs response required[] changes):
//   • REQUEST body schema: ADDING a required[] member is a break (old clients
//     don't send it → rejected).
//   • RESPONSE schema: REMOVING a required[] member is a break (old clients
//     may rely on the field being present).
// Both are detected here.

import type { BreakingFinding } from "./breaking-finding.js";

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

type JsonVal = string | number | boolean | null | JsonVal[] | JsonObj;
type JsonObj = { [k: string]: JsonVal };

interface OpenApiDoc {
  readonly paths?: Record<string, Record<string, unknown>>;
  readonly components?: {
    readonly schemas?: Record<string, unknown>;
  };
}

interface SchemaObject {
  readonly type?: string;
  readonly properties?: Record<string, unknown>;
  readonly required?: string[];
  readonly enum?: unknown[];
  readonly $ref?: string;
}

// ---------------------------------------------------------------------------
// $ref resolution
// ---------------------------------------------------------------------------

/**
 * Resolve a `$ref` value against `components.schemas` of the same document.
 * Only supports local `#/components/schemas/<name>` refs.
 * Returns undefined for external refs (not supported) or dangling refs.
 */
function resolveRef(
  ref: string,
  doc: OpenApiDoc,
  filePath: string,
): SchemaObject | { readonly __dangling: string } {
  const prefix = "#/components/schemas/";

  if (!ref.startsWith(prefix)) {
    // External or non-schema ref — not supported; treat as non-structural.
    return { __dangling: ref };
  }

  const name = ref.slice(prefix.length);
  const schema = doc.components?.schemas?.[name];

  if (schema === undefined) {
    return {
      __dangling: `${filePath}: dangling $ref '${ref}' — schema '${name}' not found in components.schemas`,
    };
  }

  return schema as SchemaObject;
}

function isDangling(
  s: SchemaObject | { __dangling: string },
): s is { __dangling: string } {
  return "__dangling" in s;
}

function resolveSchema(
  raw: unknown,
  doc: OpenApiDoc,
  filePath: string,
): SchemaObject | { readonly __dangling: string } {
  if (raw === null || typeof raw !== "object" || Array.isArray(raw))
    return {} as SchemaObject;

  const obj = raw as JsonObj;

  if (typeof obj["$ref"] === "string") {
    return resolveRef(obj["$ref"], doc, filePath);
  }

  return obj as SchemaObject;
}

// ---------------------------------------------------------------------------
// HTTP method constants (subset of OpenAPI-valid methods)
// ---------------------------------------------------------------------------

const HTTP_METHODS = new Set([
  "get",
  "put",
  "post",
  "delete",
  "options",
  "head",
  "patch",
  "trace",
]);

function isHttpMethod(key: string): boolean {
  return HTTP_METHODS.has(key.toLowerCase());
}

// ---------------------------------------------------------------------------
// Public API
// ---------------------------------------------------------------------------

/**
 * Diff two OpenAPI 3.0 documents and return breaking-change findings.
 *
 * @param before    - Parsed baseline OpenAPI document.
 * @param after     - Parsed proposed OpenAPI document.
 * @param filePath  - Source file path for finding messages.
 * @returns Array of breaking findings (empty = no breaks detected).
 * @throws {Error} When either document has a malformed structure (fail-loud).
 */
export function diffOpenApi(
  before: unknown,
  after: unknown,
  filePath: string,
): BreakingFinding[] {
  if (before === undefined || before === null) {
    // New file → fully additive.
    return [];
  }

  if (typeof before !== "object" || Array.isArray(before)) {
    throw new Error(
      `[openapi-diff] ${filePath}: baseline is not a JSON object`,
    );
  }

  if (typeof after !== "object" || Array.isArray(after) || after === null) {
    throw new Error(
      `[openapi-diff] ${filePath}: proposed is not a JSON object`,
    );
  }

  const beforeDoc = before as OpenApiDoc;
  const afterDoc = after as OpenApiDoc;

  const findings: BreakingFinding[] = [];

  // ── 1. Removed component schemas ────────────────────────────────────────
  const beforeSchemas = beforeDoc.components?.schemas ?? {};
  const afterSchemas = afterDoc.components?.schemas ?? {};

  for (const schemaName of Object.keys(beforeSchemas)) {
    if (afterSchemas[schemaName] === undefined) {
      findings.push({
        arm: "openapi",
        severity: "ERROR",
        file: filePath,
        message:
          `✗ BREAKING: ${filePath}\n` +
          `  Component schema '${schemaName}' was removed from components.schemas.\n` +
          `  Gate FAILED — schema removed without force valve.`,
      });
    }
  }

  // ── 2. Schema property diffs (response narrowing + type/enum changes) ───
  for (const schemaName of Object.keys(beforeSchemas)) {
    if (afterSchemas[schemaName] === undefined) continue; // already reported above

    const bSchema = beforeSchemas[schemaName] as SchemaObject | undefined;
    const aSchema = afterSchemas[schemaName] as SchemaObject | undefined;

    if (bSchema === undefined || aSchema === undefined) continue;

    // Response narrowing: required[] member removed.
    const bRequired = bSchema.required ?? [];
    const aRequired = new Set(aSchema.required ?? []);

    for (const field of bRequired) {
      if (!aRequired.has(field)) {
        findings.push({
          arm: "openapi",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Schema '${schemaName}': required field '${field}' was removed.\n` +
            `  Old clients may rely on this field being present in responses.\n` +
            `  Gate FAILED — required field removed without force valve.`,
        });
      }
    }

    // Response narrowing: property removed entirely.
    const bProps = bSchema.properties ?? {};
    const aProps = aSchema.properties ?? {};

    for (const propName of Object.keys(bProps)) {
      if (aProps[propName] === undefined) {
        findings.push({
          arm: "openapi",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Schema '${schemaName}': property '${propName}' was removed.\n` +
            `  Gate FAILED — property removed without force valve.`,
        });

        continue;
      }

      const bProp = bProps[propName] as SchemaObject;
      const aProp = aProps[propName] as SchemaObject;

      // Type narrowing.
      if (
        bProp.type !== undefined &&
        aProp.type !== undefined &&
        bProp.type !== aProp.type
      ) {
        findings.push({
          arm: "openapi",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Schema '${schemaName}'.${propName}: type changed from '${bProp.type}' to '${aProp.type}'.\n` +
            `  Gate FAILED — type narrowing without force valve.`,
        });
      }

      // Dropped enum value.
      if (Array.isArray(bProp.enum) && Array.isArray(aProp.enum)) {
        const aEnumSet = new Set(aProp.enum.map((v) => JSON.stringify(v)));

        for (const val of bProp.enum) {
          if (!aEnumSet.has(JSON.stringify(val))) {
            findings.push({
              arm: "openapi",
              severity: "ERROR",
              file: filePath,
              message:
                `✗ BREAKING: ${filePath}\n` +
                `  Schema '${schemaName}'.${propName}: enum value ${JSON.stringify(val)} was removed.\n` +
                `  Clients that send/receive this value will break.\n` +
                `  Gate FAILED — enum value dropped without force valve.`,
            });
          }
        }
      }
    }
  }

  // ── 3. Path + operation diffs ────────────────────────────────────────────
  const beforePaths = beforeDoc.paths ?? {};
  const afterPaths = afterDoc.paths ?? {};

  for (const path of Object.keys(beforePaths)) {
    if (afterPaths[path] === undefined) {
      findings.push({
        arm: "openapi",
        severity: "ERROR",
        file: filePath,
        message:
          `✗ BREAKING: ${filePath}\n` +
          `  Path '${path}' was removed.\n` +
          `  Gate FAILED — path removed without force valve.`,
      });

      continue;
    }

    const beforeOps = beforePaths[path] as Record<string, unknown>;
    const afterOps = afterPaths[path] as Record<string, unknown>;

    for (const method of Object.keys(beforeOps)) {
      if (!isHttpMethod(method)) continue;

      if (afterOps[method] === undefined) {
        findings.push({
          arm: "openapi",
          severity: "ERROR",
          file: filePath,
          message:
            `✗ BREAKING: ${filePath}\n` +
            `  Operation '${method.toUpperCase()} ${path}' was removed.\n` +
            `  Gate FAILED — operation removed without force valve.`,
        });

        continue;
      }

      const beforeOp = beforeOps[method] as Record<string, unknown>;
      const afterOp = afterOps[method] as Record<string, unknown>;

      // ── Request strengthening: ADDING a required field to request body ──
      const beforeReqBody = beforeOp["requestBody"] as
        | Record<string, unknown>
        | undefined;
      const afterReqBody = afterOp["requestBody"] as
        | Record<string, unknown>
        | undefined;

      if (beforeReqBody !== undefined && afterReqBody !== undefined) {
        const bContent = beforeReqBody["content"] as
          | Record<string, unknown>
          | undefined;
        const aContent = afterReqBody["content"] as
          | Record<string, unknown>
          | undefined;

        if (bContent !== undefined && aContent !== undefined) {
          for (const mediaType of Object.keys(bContent)) {
            const bMedia = bContent[mediaType] as
              | Record<string, unknown>
              | undefined;
            const aMedia = aContent[mediaType] as
              | Record<string, unknown>
              | undefined;

            if (bMedia === undefined || aMedia === undefined) continue;

            const bSchemaRaw = bMedia["schema"];
            const aSchemaRaw = aMedia["schema"];

            const bReqSchema = resolveSchema(bSchemaRaw, beforeDoc, filePath);
            const aReqSchema = resolveSchema(aSchemaRaw, afterDoc, filePath);

            if (isDangling(aReqSchema)) {
              findings.push({
                arm: "openapi",
                severity: "ERROR",
                file: filePath,
                message:
                  `✗ BREAKING: ${filePath}\n` +
                  `  Operation '${method.toUpperCase()} ${path}' (${mediaType}): ${aReqSchema.__dangling}\n` +
                  `  Gate FAILED — dangling $ref in request schema.`,
              });

              continue;
            }

            if (isDangling(bReqSchema)) continue; // baseline dangling → not our problem to enforce

            const bReqSchemaResolved = bReqSchema as SchemaObject;
            const aReqSchemaResolved = aReqSchema as SchemaObject;
            const bReqRequired = new Set(bReqSchemaResolved.required ?? []);
            const aReqRequired = aReqSchemaResolved.required ?? [];

            for (const field of aReqRequired) {
              if (!bReqRequired.has(field)) {
                findings.push({
                  arm: "openapi",
                  severity: "ERROR",
                  file: filePath,
                  message:
                    `✗ BREAKING: ${filePath}\n` +
                    `  Operation '${method.toUpperCase()} ${path}' (${mediaType}): new required request field '${field}' added.\n` +
                    `  Old clients that don't send this field will be rejected.\n` +
                    `  Gate FAILED — required request field added without force valve.`,
                });
              }
            }
          }
        }
      }
    }
  }

  return findings;
}
