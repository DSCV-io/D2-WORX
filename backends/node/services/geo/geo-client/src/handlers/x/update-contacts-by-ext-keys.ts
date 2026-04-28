import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import { handleGrpcCall } from "@d2/result-extensions";
import type { GeoServiceClient, UpdateContactsByExtKeysResponse } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { Metadata } from "@grpc/grpc-js";
import { z } from "zod";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { GEO_CACHE_KEYS } from "../../cache-keys.js";
import { Complex } from "../../interfaces/index.js";

type Input = Complex.UpdateContactsByExtKeysInput;
type Output = Complex.UpdateContactsByExtKeysOutput;

/**
 * Handler for replacing Geo contacts at given ext keys via gRPC.
 * Geo internally deletes old contacts and creates new ones atomically.
 * Evicts ext-key cache for each input contact's key.
 * Mirrors D2.Geo.Client.CQRS.Handlers.X.UpdateContactsByExtKeys in .NET.
 */
export class UpdateContactsByExtKeys
  extends BaseHandler<Input, Output>
  implements Complex.IUpdateContactsByExtKeysHandler
{
  override get redaction() {
    return Complex.UPDATE_CONTACTS_BY_EXT_KEYS_REDACTION;
  }

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
        contacts: z.array(
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
        new Promise<UpdateContactsByExtKeysResponse>((resolve, reject) => {
          this.geoClient.updateContactsByExtKeys(
            { contacts: input.contacts },
            new Metadata(),
            { deadline: Date.now() + this.options.grpcTimeoutMs },
            (err, res) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ replacements: res.replacements ?? [] }),
    );

    // Evict ext-key cache for each input contact regardless of gRPC result
    for (const c of input.contacts) {
      if (c.contextKey && c.relatedEntityId) {
        this.store.delete(GEO_CACHE_KEYS.contactsByExtKey(c.contextKey, c.relatedEntityId));
      }
    }

    return D2Result.bubble(r, r.data);
  }
}

export type {
  UpdateContactsByExtKeysInput,
  UpdateContactsByExtKeysOutput,
} from "../../interfaces/x/update-contacts-by-ext-keys.js";
