import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { GeoServiceClient, GetContactsByExtKeysResponse, ContactDTO } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { Metadata } from "@grpc/grpc-js";
import { z } from "zod";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { GEO_CACHE_KEYS } from "../../cache-keys.js";
import { Queries } from "../../interfaces/index.js";

type Input = Queries.GetContactsByExtKeysInput;
type Output = Queries.GetContactsByExtKeysOutput;

/**
 * Handler for fetching Geo contacts by ext keys with local cache-aside.
 * Contacts are immutable, so cached entries never expire (only LRU eviction).
 * Fail-open: returns whatever was cached if gRPC fails.
 * Mirrors D2.Geo.Client.CQRS.Handlers.Q.GetContactsByExtKeys in .NET.
 */
export class GetContactsByExtKeys
  extends BaseHandler<Input, Output>
  implements Queries.IGetContactsByExtKeysHandler
{
  override get redaction() {
    return Queries.GET_CONTACTS_BY_EXT_KEYS_REDACTION;
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

    if (input.keys.length === 0) {
      return D2Result.ok({ data: { data: new Map() } });
    }

    const result = new Map<string, ContactDTO[]>();
    const missingKeys: Array<{ contextKey: string; relatedEntityId: string }> = [];

    // Check cache first
    for (const key of input.keys) {
      const cacheKey = GEO_CACHE_KEYS.contactsByExtKey(key.contextKey, key.relatedEntityId);
      const cached = this.store.get<ContactDTO[]>(cacheKey);
      if (cached !== undefined) {
        result.set(`${key.contextKey}:${key.relatedEntityId}`, cached);
      } else {
        missingKeys.push(key);
      }
    }

    // All cached — return early
    if (missingKeys.length === 0) {
      return D2Result.ok({ data: { data: result } });
    }

    // Fetch cache misses from Geo service
    let response: GetContactsByExtKeysResponse;
    try {
      response = await new Promise<GetContactsByExtKeysResponse>((resolve, reject) => {
        this.geoClient.getContactsByExtKeys(
          { keys: missingKeys },
          new Metadata(),
          { deadline: Date.now() + this.options.grpcTimeoutMs },
          (err, res) => {
            if (err) reject(err);
            else resolve(res);
          },
        );
      });
    } catch (err: unknown) {
      // Fail-open: return whatever was cached
      this.context.logger.warn(
        `gRPC call to Geo service failed for GetContactsByExtKeys. TraceId: ${this.traceId}`,
        { error: err instanceof Error ? err.message : String(err) },
      );
      return D2Result.ok({ data: { data: result } });
    }

    if (response.result?.success && response.data) {
      // Cache each fetched result (no TTL — contacts are immutable, LRU evicts)
      for (const entry of response.data) {
        if (entry.key?.contextKey && entry.key?.relatedEntityId) {
          const { contextKey, relatedEntityId } = entry.key;
          const mapKey = `${contextKey}:${relatedEntityId}`;
          const cacheKey = GEO_CACHE_KEYS.contactsByExtKey(contextKey, relatedEntityId);
          const contacts = entry.contacts ?? [];
          this.store.set(cacheKey, contacts);
          result.set(mapKey, contacts);
        }
      }
    }

    return D2Result.ok({ data: { data: result } });
  }
}

export type {
  GetContactsByExtKeysInput,
  GetContactsByExtKeysOutput,
} from "../../interfaces/q/get-contacts-by-ext-keys.js";
