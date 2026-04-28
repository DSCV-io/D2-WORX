import { D2Result, type InputError, type HttpStatusCode } from "@d2/result";
import type { D2ResultProto } from "@d2/protos";

/**
 * Convert a D2ResultProto back to a D2Result.
 * Mirrors D2.Shared.Result.Extensions.ProtoExtensions.ToD2Result() in .NET.
 */
export function d2ResultFromProto<TData = void>(
  proto: D2ResultProto,
  data?: TData,
): D2Result<TData> {
  return new D2Result<TData>({
    success: proto.success ?? false,
    data,
    messages: [...(proto.messages ?? [])],
    inputErrors: (proto.inputErrors ?? []).map(
      (ie): InputError => [ie.field ?? "", ...(ie.errors ?? [])],
    ),
    statusCode: proto.statusCode as HttpStatusCode,
    errorCode: proto.errorCode || undefined,
    traceId: proto.traceId || undefined,
  });
}
