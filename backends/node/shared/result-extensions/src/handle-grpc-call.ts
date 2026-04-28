import { D2Result } from "@d2/result";
import type { D2ResultProto } from "@d2/protos";
import type { ServiceError } from "@grpc/grpc-js";
import { d2ResultFromProto } from "./d2-result-from-proto.js";

/**
 * Type guard for gRPC ServiceError.
 * ServiceError extends Error with a numeric `code` (gRPC status).
 */
function isServiceError(err: unknown): err is ServiceError {
  return err instanceof Error && "code" in err && typeof (err as ServiceError).code === "number";
}

/**
 * Execute a gRPC unary call and convert the response to D2Result.
 * Mirrors D2.Shared.Result.Extensions.ProtoExtensions.HandleAsync() in .NET.
 *
 * @param callFn - Function that returns a promise resolving the gRPC response.
 * @param resultSelector - Extracts D2ResultProto from the response.
 * @param dataSelector - Extracts typed data from the response.
 */
export async function handleGrpcCall<TResponse, TData>(
  callFn: () => Promise<TResponse>,
  resultSelector: (response: TResponse) => D2ResultProto,
  dataSelector: (response: TResponse) => TData | undefined,
): Promise<D2Result<TData>> {
  try {
    const response = await callFn();
    const proto = resultSelector(response);
    const data = dataSelector(response);
    return d2ResultFromProto<TData>(proto, data);
  } catch (err: unknown) {
    if (isServiceError(err)) {
      return D2Result.serviceUnavailable<TData>({
        messages: [`gRPC call failed with status ${err.code}: ${err.details || err.message}`],
      });
    }
    return D2Result.unhandledException<TData>({
      messages: [`gRPC call failed: ${err instanceof Error ? err.message : String(err)}`],
    });
  }
}
