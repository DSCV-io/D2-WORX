import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import { handleGrpcCall } from "@d2/result-extensions";
import type { GeoServiceClient, DeleteContactsByExtKeysResponse } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { Metadata } from "@grpc/grpc-js";
import { z } from "zod";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { GEO_CACHE_KEYS } from "../../cache-keys.js";
import type { Commands } from "../../interfaces/index.js";

type Input = Commands.DeleteContactsByExtKeysInput;
type Output = Commands.DeleteContactsByExtKeysOutput;

/**
 * Handler for deleting Geo contacts by ext keys via gRPC.
 * Evicts deleted contacts from the local ext-key cache.
 * Mirrors D2.Geo.Client.CQRS.Handlers.C.DeleteContactsByExtKeys in .NET.
 */
export class DeleteContactsByExtKeys
  extends BaseHandler<Input, Output>
  implements Commands.IDeleteContactsByExtKeysHandler
{
  private readonly store: MemoryCacheStore;
  private readonly geoClient: GeoServiceClient;
  private readonly options: GeoClientOptions;
  private readonly inputSchema: z.ZodType<Input>;

  constructor(
    store: MemoryCacheStore,
    geoClient: GeoServiceClient,
    options: GeoClientOptions,
    context: IHandlerContext,
  ) {
    super(context);
    this.store = store;
    this.geoClient = geoClient;
    this.options = options;
    this.inputSchema = z
      .object({
        keys: z.array(
          z
            .object({ contextKey: validators.zodAllowedContextKey(options.allowedContextKeys) })
            .passthrough(),
        ),
      })
      .passthrough() as unknown as z.ZodType<Input>;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(this.inputSchema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const r = await handleGrpcCall(
      () =>
        new Promise<DeleteContactsByExtKeysResponse>((resolve, reject) => {
          this.geoClient.deleteContactsByExtKeys(
            { keys: input.keys },
            new Metadata(),
            { deadline: Date.now() + this.options.grpcTimeoutMs },
            (err, res) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ deleted: res.deleted ?? 0 }),
    );

    // Evict ext-key cache for each input key regardless of gRPC result
    for (const key of input.keys) {
      this.store.delete(GEO_CACHE_KEYS.contactsByExtKey(key.contextKey, key.relatedEntityId));
    }

    return D2Result.bubble(r, r.data);
  }
}

export type {
  DeleteContactsByExtKeysInput,
  DeleteContactsByExtKeysOutput,
} from "../../interfaces/c/delete-contacts-by-ext-keys.js";
