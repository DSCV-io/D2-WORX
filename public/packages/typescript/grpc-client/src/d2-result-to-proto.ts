// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import type { D2Result, InputError } from "@d2/result";
import { D2ResultProto, InputErrorProto, TKMessageProto } from "@d2/protos";

/**
 * Convert a `D2Result` to its `D2ResultProto` wire representation.
 * Mirrors .NET `D2.Shared.Result.Grpc.ProtoExtensions.ToProto()`.
 *
 * Mapping rules:
 * - `success`, `statusCode` — always present.
 * - `errorCode`, `category`, `traceId` — optional (proto3 optional); absent when undefined.
 * - `messages` — `TKMessage[]` → `TKMessageProto[]` (key + params as string map).
 * - `inputErrors` — `InputError[]` → `InputErrorProto[]` (field + TKMessageProto errors).
 */
export function d2ResultToProto(result: D2Result<unknown>): D2ResultProto {
  return D2ResultProto.create({
    success: result.success,
    statusCode: result.statusCode,
    errorCode: result.errorCode,
    category: result.category,
    traceId: result.traceId,
    messages: result.messages.map((m) =>
      TKMessageProto.create({
        key: m.key,
        params: _paramsToStringMap(m.params),
      }),
    ),
    inputErrors: result.inputErrors.map((ie: InputError) =>
      InputErrorProto.create({
        field: ie.field,
        errors: ie.errors.map((e) =>
          TKMessageProto.create({
            key: e.key,
            params: _paramsToStringMap(e.params),
          }),
        ),
      }),
    ),
  });
}

/**
 * Converts optional `params` (`Readonly<Record<string, unknown>>`) to the
 * proto wire map (`{ [k: string]: string }`). All values are coerced to
 * strings — the proto contract specifies `map<string,string>`.
 */
function _paramsToStringMap(
  params: Readonly<Record<string, unknown>> | undefined,
): { [k: string]: string } {
  if (params === undefined) return {};
  const result: { [k: string]: string } = {};
  for (const [k, v] of Object.entries(params)) result[k] = String(v);
  return result;
}
