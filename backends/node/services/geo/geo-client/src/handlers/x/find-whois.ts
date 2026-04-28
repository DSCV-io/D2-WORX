import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import { type CircuitBreaker, CircuitState, type Singleflight } from "@d2/utilities";
import type { WhoIsDTO, FindWhoIsRequest, FindWhoIsResponse, GeoServiceClient } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { Metadata } from "@grpc/grpc-js";
import { z } from "zod";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { GEO_CACHE_KEYS } from "../../cache-keys.js";
import { Complex } from "../../interfaces/index.js";

type Input = Complex.FindWhoIsInput;
type Output = Complex.FindWhoIsOutput;

/**
 * Handler for finding WhoIs data by IP address.
 * Checks local LRU memory cache first, falls back to Geo service gRPC.
 * Singleflight deduplicates concurrent gRPC calls for the same cache key.
 * Fail-open: never returns error results, always Ok with nullable WhoIsDTO.
 *
 * Mirrors D2.Geo.Client.CQRS.Handlers.X.FindWhoIs in .NET.
 */
export class FindWhoIs extends BaseHandler<Input, Output> implements Complex.IFindWhoIsHandler {
  override get redaction() {
    return Complex.FIND_WHOIS_REDACTION;
  }

  private readonly store: MemoryCacheStore;
  private readonly geoClient: GeoServiceClient;
  private readonly options: GeoClientOptions;
  private readonly circuitBreaker: CircuitBreaker;
  private readonly singleflight: Singleflight;

  constructor(
    store: MemoryCacheStore,
    geoClient: GeoServiceClient,
    options: GeoClientOptions,
    circuitBreaker: CircuitBreaker,
    singleflight: Singleflight,
    context: IHandlerContext,
  ) {
    super(context);
    this.store = store;
    this.geoClient = geoClient;
    this.options = options;
    this.circuitBreaker = circuitBreaker;
    this.singleflight = singleflight;
  }

  private static readonly findWhoIsSchema = z.object({
    ipAddress: validators.zodIpAddress,
  }) as z.ZodType<Input>;

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    // Validate input.
    const validation = this.validateInput(FindWhoIs.findWhoIsSchema, input);
    if (validation.failed) {
      return D2Result.bubbleFail(validation);
    }

    const cacheKey = GEO_CACHE_KEYS.whois(input.ipAddress);

    // Try cache first (null = negative cache sentinel, undefined = miss)
    const cached = this.store.get<WhoIsDTO | null>(cacheKey);
    if (cached !== undefined) {
      return D2Result.ok({ data: { whoIs: cached ?? undefined } });
    }

    // Cache miss — call Geo service.
    // Singleflight deduplicates concurrent requests for the same cache key.
    // Circuit breaker protects against sustained downstream failures.
    let response: FindWhoIsResponse;
    try {
      const request: FindWhoIsRequest = {
        requests: [{ ipAddress: input.ipAddress }],
      };
      response = await this.singleflight.execute(cacheKey, () =>
        this.circuitBreaker.execute(
          () =>
            new Promise<FindWhoIsResponse>((resolve, reject) => {
              this.geoClient.findWhoIs(
                request,
                new Metadata(),
                { deadline: Date.now() + this.options.grpcTimeoutMs },
                (err, res) => {
                  if (err) reject(err);
                  else resolve(res);
                },
              );
            }),
        ),
      );
    } catch (err: unknown) {
      // Fail-open: handles gRPC errors AND circuit-open rejections identically.
      // When the circuit is open this returns instantly (no timeout wait).
      // Do not log input.ipAddress directly — it bypasses BaseHandler's redaction.
      if (this.circuitBreaker.state !== CircuitState.OPEN) {
        this.context.logger.warn(`gRPC call to Geo service failed. TraceId: ${this.traceId}`, {
          error: err instanceof Error ? err.message : String(err),
        });
      }
      return D2Result.ok({ data: { whoIs: undefined } });
    }

    const responseData = response.data ?? [];
    if (!response.result?.success || responseData.length === 0) {
      // Negative cache: avoid repeated gRPC calls for unknown IPs
      this.store.set(cacheKey, null, this.options.whoIsNegativeCacheExpirationMs);
      return D2Result.ok({ data: { whoIs: undefined } });
    }

    const whoIs = responseData[0]?.whois;

    // Cache the result
    if (whoIs !== undefined) {
      this.store.set(cacheKey, whoIs, this.options.whoIsCacheExpirationMs);
    }

    return D2Result.ok({ data: { whoIs } });
  }
}

export type { FindWhoIsInput, FindWhoIsOutput } from "../../interfaces/x/find-whois.js";
