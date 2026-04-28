import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { GeoServiceClient, GetContactsResponse, ContactDTO } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { Metadata } from "@grpc/grpc-js";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { GEO_CACHE_KEYS } from "../../cache-keys.js";
import { Queries } from "../../interfaces/index.js";

type Input = Queries.GetContactsByIdsInput;
type Output = Queries.GetContactsByIdsOutput;

/**
 * Handler for fetching Geo contacts by their direct IDs with local cache-aside.
 * Contacts are immutable, so cached entries never expire (only LRU eviction).
 *
 * Return semantics (mirrors .NET GetContactsByIds):
 * - `ok`        — all requested contacts found
 * - `someFound` — partial results (some IDs not resolved); data still returned
 * - `notFound`  — none of the requested contacts found
 */
export class GetContactsByIds
  extends BaseHandler<Input, Output>
  implements Queries.IGetContactsByIdsHandler
{
  override get redaction() {
    return Queries.GET_CONTACTS_BY_IDS_REDACTION;
  }

  private readonly store: MemoryCacheStore;
  private readonly geoClient: GeoServiceClient;
  private readonly options: GeoClientOptions;

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
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    if (input.ids.length === 0) {
      return D2Result.ok({ data: { data: new Map() } });
    }

    const result = new Map<string, ContactDTO>();
    const missingIds: string[] = [];

    // Check cache first
    for (const id of input.ids) {
      const cacheKey = GEO_CACHE_KEYS.contact(id);
      const cached = this.store.get<ContactDTO>(cacheKey);
      if (cached !== undefined) {
        result.set(id, cached);
      } else {
        missingIds.push(id);
      }
    }

    // All cached — return early
    if (missingIds.length === 0) {
      return D2Result.ok({ data: { data: result } });
    }

    // Fetch cache misses from Geo service
    let response: GetContactsResponse;
    try {
      response = await new Promise<GetContactsResponse>((resolve, reject) => {
        this.geoClient.getContacts(
          { ids: missingIds },
          new Metadata(),
          { deadline: Date.now() + this.options.grpcTimeoutMs },
          (err, res) => {
            if (err) reject(err);
            else resolve(res);
          },
        );
      });
    } catch (err: unknown) {
      // gRPC failed — return whatever was cached with appropriate status
      this.context.logger.warn(
        `gRPC call to Geo service failed for GetContactsByIds. TraceId: ${this.traceId}`,
        { error: err instanceof Error ? err.message : String(err) },
      );
      return this.resolveStatus(input.ids.length, result);
    }

    // Process data whenever present — server returns data even for SOME_FOUND
    if (response.data) {
      for (const [id, contact] of Object.entries(response.data)) {
        const cacheKey = GEO_CACHE_KEYS.contact(id);
        this.store.set(cacheKey, contact);
        result.set(id, contact);
      }
    }

    return this.resolveStatus(input.ids.length, result);
  }

  /**
   * Maps the collected result set to the correct D2Result status:
   * - All found → ok
   * - Some found → someFound (data attached)
   * - None found → notFound
   */
  private resolveStatus(
    requestedCount: number,
    result: Map<string, ContactDTO>,
  ): D2Result<Output | undefined> {
    if (result.size === requestedCount) {
      return D2Result.ok({ data: { data: result } });
    }
    if (result.size > 0) {
      return D2Result.someFound({ data: { data: result } });
    }
    return D2Result.notFound();
  }
}

export type {
  GetContactsByIdsInput,
  GetContactsByIdsOutput,
} from "../../interfaces/q/get-contacts-by-ids.js";
