// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { D2Result, type InputError } from "@d2/result";
import type { D2ResultProto } from "@d2/protos";
import { type ErrorCategory, ALL_ERROR_CATEGORIES } from "@d2/error-category";
import { tk } from "@d2/i18n-abstractions";
import { truthyOrUndefined } from "@d2/utilities";

/**
 * Convert a `D2ResultProto` back to a `D2Result<TData>`, optionally stitching
 * in separately-selected typed payload data.
 *
 * Mirrors .NET `D2.Shared.Result.Grpc.ProtoExtensions.ToD2Result<TData>()`.
 *
 * Mapping rules:
 * - `success`, `statusCode` — always carried.
 * - `errorCode`, `traceId` — rehydrated via `truthyOrUndefined`; absent/empty → undefined.
 * - `category` — proto string → `ErrorCategory` union; unknown/absent wire string → undefined
 *   (guard: never throws on a bad wire value).
 * - `messages` — `TKMessageProto[]` → `TKMessage[]` via `tk(key, params)`;
 *   `params: {[k:string]:string}` widened to `Record<string,unknown>`
 *   (safe — string ⊆ unknown).
 * - `inputErrors` — `InputErrorProto[]` → `InputError[]` (field + TKMessage[]).
 * - `data` — threaded as-is from the caller's data selector.
 */
export function d2ResultFromProto<TData>(
  proto: D2ResultProto,
  data?: TData,
): D2Result<TData> {
  return new D2Result<TData>({
    success: proto.success,
    data,
    messages: proto.messages.map((m) =>
      tk(m.key, _protoParamsToRecord(m.params)),
    ),
    inputErrors: proto.inputErrors.map(
      (ie): InputError => ({
        field: ie.field,
        errors: ie.errors.map((e) => tk(e.key, _protoParamsToRecord(e.params))),
      }),
    ),
    statusCode: proto.statusCode as D2Result["statusCode"],
    errorCode: truthyOrUndefined(proto.errorCode),
    traceId: truthyOrUndefined(proto.traceId),
    category: _parseCategory(proto.category),
  });
}

/**
 * Parse a proto `category` string to a typed `ErrorCategory` union member.
 * Returns `undefined` for absent/unknown wire values — never throws.
 */
function _parseCategory(wire: string | undefined): ErrorCategory | undefined {
  if (!wire) return undefined;
  return (ALL_ERROR_CATEGORIES as readonly string[]).includes(wire)
    ? (wire as ErrorCategory)
    : undefined;
}

/**
 * Convert the proto `map<string,string>` params to `Record<string,unknown>`.
 * The widening is safe — string satisfies unknown. Returns undefined when the
 * map is empty so `TKMessage.params` stays absent.
 */
function _protoParamsToRecord(params: {
  [k: string]: string;
}): Readonly<Record<string, unknown>> | undefined {
  const entries = Object.entries(params);
  if (entries.length === 0) return undefined;
  const result: Record<string, unknown> = {};
  for (const [k, v] of entries) result[k] = v;
  return result;
}
