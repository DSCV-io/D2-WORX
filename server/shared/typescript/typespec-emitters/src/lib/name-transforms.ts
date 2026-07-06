// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Name-transform utilities for emitting cross-language identifiers.
//
// Both regexes are linear with no super-linear backtracking and operate on
// bounded-length identifier strings (Bucket 2 per regex-redos-discipline —
// no matchTimeout and no JIT pre-warm are needed; the input cannot grow
// unboundedly and neither pattern has nested quantifiers).

/**
 * Convert a lowerCamelCase or PascalCase identifier to lower_snake_case.
 *
 * proto3 field names are lower_snake_case by convention (the spec-driven
 * emitters use this for proto field and JSON property names).
 *
 * @example toSnake("myFieldName") // "my_field_name"
 * @example toSnake("id2Code")     // "id2_code"
 */
export function toSnake(s: string): string {
  return s.replace(/([a-z0-9])([A-Z])/g, "$1_$2").toLowerCase();
}

/**
 * Convert a lower_snake_case or lowerCamelCase identifier to PascalCase.
 *
 * C# property and type names are PascalCase; Grpc.Tools derives C# property
 * names from proto field names by PascalCasing them, so this function is the
 * inverse of toSnake for generated C# consumers.
 *
 * @example toPascal("my_field_name")  // "MyFieldName"
 * @example toPascal("myFieldName")    // "MyFieldName"
 */
export function toPascal(s: string): string {
  return s.replace(/(^|_)([a-z0-9])/g, (_, __, c: string) => c.toUpperCase());
}

/**
 * Convert a lowerCamelCase or PascalCase identifier to kebab-case.
 *
 * Used for TS generated file names (`<kebab-op>-dto.g.ts`) and concern-folder
 * segments (`@d2Concern("CaCertificate")` → `ca-certificate/`). The concern
 * folder a DTO is written to and the concern-relative import specifier the gRPC
 * client emits MUST derive from this one function so folder and import agree.
 *
 * @example toKebab("getKeyring")    // "get-keyring"
 * @example toKebab("CaCertificate") // "ca-certificate"
 */
export function toKebab(s: string): string {
  return s.replace(/([a-z0-9])([A-Z])/g, "$1-$2").toLowerCase();
}
