import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import { type DistributedCache, RateLimit } from "@d2/interfaces";
import { isLocalhost } from "@d2/request-enrichment";
import { z } from "zod";
import { DEFAULT_RATE_LIMIT_OPTIONS, type RateLimitOptions } from "../rate-limit-options.js";
import { RATELIMIT_CACHE_KEYS } from "../cache-keys.js";

type CheckInput = RateLimit.CheckInput;
type CheckOutput = RateLimit.CheckOutput;

/**
 * Handler for checking rate limits using sliding window approximation.
 * Mirrors D2.Shared.RateLimit.Default.Handlers.Check in .NET.
 *
 * Uses two fixed-window counters per dimension with a weighted average
 * to approximate a sliding window. Uses distributed cache handlers for
 * storage abstraction.
 *
 * Fail-open on all cache errors.
 */
export class CheckRateLimit
  extends BaseHandler<CheckInput, CheckOutput>
  implements RateLimit.ICheckHandler
{
  override get redaction() {
    return RateLimit.CHECK_REDACTION;
  }

  private readonly getTtl: DistributedCache.IGetTtlHandler;
  private readonly increment: DistributedCache.IIncrementHandler;
  private readonly set: DistributedCache.ISetHandler<string>;
  private readonly options: RateLimitOptions;

  constructor(
    getTtl: DistributedCache.IGetTtlHandler,
    increment: DistributedCache.IIncrementHandler,
    set: DistributedCache.ISetHandler<string>,
    options: Partial<RateLimitOptions>,
    context: IHandlerContext,
  ) {
    super(context);
    this.getTtl = getTtl;
    this.increment = increment;
    this.set = set;
    this.options = { ...DEFAULT_RATE_LIMIT_OPTIONS, ...options };
  }

  private static readonly checkSchema = z.object({
    requestContext: z
      .object({
        clientIp: z.union([validators.zodIpAddress, z.literal("unknown")]).optional(),
        deviceFingerprint: z.string().length(64).optional(),
        countryCode: z.string().length(2).optional(),
      })
      .passthrough(),
  }) as unknown as z.ZodType<CheckInput>;

  protected async executeAsync(input: CheckInput): Promise<D2Result<CheckOutput | undefined>> {
    // Trusted services bypass all rate limiting (mirrors .NET Check handler).
    if (input.requestContext.isTrustedService) {
      this.context.logger.info(`Rate limit bypassed for trusted service. TraceId: ${this.traceId}`);
      return D2Result.ok({
        data: { isBlocked: false, blockedDimension: undefined, retryAfterMs: undefined },
      });
    }

    // Validate input.
    const validation = this.validateInput(CheckRateLimit.checkSchema, input);
    if (validation.failed) {
      return D2Result.bubbleFail(validation);
    }

    const { requestContext } = input;

    // Build the list of applicable dimension checks, then fire them all concurrently.
    // Each dimension's internal Redis operations (GetTtl + 2x Increment) also run
    // concurrently. This reduces total latency from N*3 sequential Redis round-trips
    // to a single round-trip time (~5-8ms regardless of dimension count).
    const checks: Promise<CheckOutput>[] = [];

    // Device fingerprint is always present when request enrichment runs.
    if (requestContext.deviceFingerprint) {
      checks.push(
        this.checkDimension(
          RateLimit.RateLimitDimension.DeviceFingerprint,
          requestContext.deviceFingerprint,
          this.options.deviceFingerprintThreshold,
        ),
      );
    }

    if (requestContext.clientIp && !isLocalhost(requestContext.clientIp)) {
      checks.push(
        this.checkDimension(
          RateLimit.RateLimitDimension.Ip,
          requestContext.clientIp,
          this.options.ipThreshold,
        ),
      );
    }

    if (requestContext.city) {
      checks.push(
        this.checkDimension(
          RateLimit.RateLimitDimension.City,
          requestContext.city,
          this.options.cityThreshold,
        ),
      );
    }

    if (
      requestContext.countryCode &&
      !this.options.whitelistedCountryCodes.includes(requestContext.countryCode)
    ) {
      checks.push(
        this.checkDimension(
          RateLimit.RateLimitDimension.Country,
          requestContext.countryCode,
          this.options.countryThreshold,
        ),
      );
    }

    const results = await Promise.all(checks);
    const blocked = results.find((r) => r.isBlocked);
    if (blocked) {
      return D2Result.ok({ data: blocked });
    }

    return D2Result.ok({
      data: { isBlocked: false, blockedDimension: undefined, retryAfterMs: undefined },
    });
  }

  /**
   * Gets the window ID for a given timestamp (minute-granularity UTC).
   */
  private static getWindowId(time: Date): string {
    const year = time.getUTCFullYear();
    const month = String(time.getUTCMonth() + 1).padStart(2, "0");
    const day = String(time.getUTCDate()).padStart(2, "0");
    const hours = String(time.getUTCHours()).padStart(2, "0");
    const minutes = String(time.getUTCMinutes()).padStart(2, "0");
    return `${year}-${month}-${day}T${hours}:${minutes}`;
  }

  /**
   * Checks a single dimension using sliding window approximation.
   */
  private async checkDimension(
    dimension: RateLimit.RateLimitDimension,
    value: string,
    threshold: number,
  ): Promise<CheckOutput> {
    const dimensionKey = dimension.toLowerCase();
    const blockedKey = RATELIMIT_CACHE_KEYS.blocked(dimensionKey, value);

    try {
      // Compute window keys upfront so all Redis operations can fire concurrently.
      const now = new Date();
      const windowSeconds = this.options.windowMs / 1000;
      const currentWindowId = CheckRateLimit.getWindowId(now);
      const previousTime = new Date(now.getTime() - this.options.windowMs);
      const previousWindowId = CheckRateLimit.getWindowId(previousTime);

      const currentKey = RATELIMIT_CACHE_KEYS.counter(dimensionKey, value, currentWindowId);
      const previousKey = RATELIMIT_CACHE_KEYS.counter(dimensionKey, value, previousWindowId);
      const counterTtlMs = this.options.windowMs * 2;

      // Fire all three Redis operations concurrently: blocked check, previous
      // window read, and current window increment. If already blocked, the
      // extra increment is harmless (counter auto-expires via TTL).
      const [ttlResult, prevResult, incrResult] = await Promise.all([
        this.getTtl.handleAsync({ key: blockedKey }),
        this.increment.handleAsync({ key: previousKey, amount: 0, expirationMs: counterTtlMs }),
        this.increment.handleAsync({ key: currentKey, amount: 1, expirationMs: counterTtlMs }),
      ]);

      // 1. Check if already blocked.
      if (ttlResult.success && ttlResult.data?.timeToLiveMs !== undefined) {
        // Do not log raw `value` — it may be an IP address or fingerprint (PII).
        this.context.logger.debug(
          `Request blocked on ${dimension} dimension. TTL: ${ttlResult.data.timeToLiveMs}ms. TraceId: ${this.traceId}`,
        );
        return {
          isBlocked: true,
          blockedDimension: dimension,
          retryAfterMs: ttlResult.data.timeToLiveMs,
        };
      }

      // 2. Evaluate increment results.
      if (!incrResult.success) {
        // Fail-open on cache errors. Do not log raw `value` — may be PII.
        this.context.logger.warn(
          `Failed to increment rate limit counter for ${dimension}. Failing open. TraceId: ${this.traceId}`,
        );
        return { isBlocked: false, blockedDimension: undefined, retryAfterMs: undefined };
      }

      const prevCount = prevResult.success ? (prevResult.data?.newValue ?? 0) : 0;
      const currCount = incrResult.data?.newValue ?? 0;

      // 3. Calculate weighted estimate.
      const secondsIntoCurrentWindow = now.getUTCSeconds() + now.getUTCMilliseconds() / 1000;
      const weight = 1.0 - secondsIntoCurrentWindow / windowSeconds;
      const estimated = Math.floor(prevCount * weight + currCount);

      // 4. Check if over threshold.
      if (estimated > threshold) {
        // Block the dimension.
        await this.set.handleAsync({
          key: blockedKey,
          value: "1",
          expirationMs: this.options.blockDurationMs,
        });

        // Do not log raw `value` — may be an IP address or fingerprint (PII).
        this.context.logger.warn(
          `Rate limit exceeded on ${dimension} dimension. Count: ${estimated}/${threshold}. TraceId: ${this.traceId}`,
        );

        return {
          isBlocked: true,
          blockedDimension: dimension,
          retryAfterMs: this.options.blockDurationMs,
        };
      }

      return { isBlocked: false, blockedDimension: undefined, retryAfterMs: undefined };
    } catch {
      // Fail-open on cache errors. Do not log raw `value` — may be PII.
      this.context.logger.warn(
        `Error checking rate limit for ${dimension}. Failing open. TraceId: ${this.traceId}`,
      );
      return { isBlocked: false, blockedDimension: undefined, retryAfterMs: undefined };
    }
  }
}
