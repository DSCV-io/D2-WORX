import { D2Result } from "@d2/result";
import type { IMessagePublisher, PublishTarget } from "./types.js";

/**
 * Execute a RabbitMQ publish and return a structured D2Result.
 * Mirrors `handleGrpcCall` from `@d2/result-extensions` for gRPC.
 *
 * On success → `ok()`. On failure → `serviceUnavailable()` with the
 * exchange/routingKey in the error message for operational visibility.
 *
 * @param publisher - The RabbitMQ publisher.
 * @param target - Exchange + routing key.
 * @param body - Message payload (serialized to JSON by the publisher).
 */
export async function handlePublish<T>(
  publisher: IMessagePublisher,
  target: PublishTarget,
  body: T,
): Promise<D2Result<void>> {
  try {
    await publisher.send(target, body);
    return D2Result.ok();
  } catch (err: unknown) {
    return D2Result.serviceUnavailable({
      messages: [
        `Failed to publish to ${target.exchange}/${target.routingKey}: ${err instanceof Error ? err.message : String(err)}`,
      ],
    });
  }
}
