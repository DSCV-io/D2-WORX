/**
 * Storage-layer options for the Files service.
 *
 * Controls presigned URL expiries. Presigned URLs are time-limited tokens that
 * grant direct browser access to MinIO/S3 — they must outlive the slowest
 * realistic upload (PUT) or browser cache cycle (GET) but stay short enough
 * to limit replay risk.
 */
export interface FilesStorageOptions {
  /** Lifetime of presigned PUT URLs (uploads). Default: 900 (15 min). */
  readonly presignPutExpirySeconds: number;
  /** Lifetime of presigned GET URLs (downloads/img src). Default: 3600 (1 hour). */
  readonly presignGetExpirySeconds: number;
}

export const DEFAULT_FILES_STORAGE_OPTIONS: FilesStorageOptions = {
  presignPutExpirySeconds: 900,
  presignGetExpirySeconds: 3600,
};

/**
 * Scanning-provider options for the Files service.
 *
 * Controls clamd TCP socket behavior. clamd connections are single-shot
 * (one INSTREAM scan per connection); the timeout protects against hung
 * scanners blocking the processing pipeline.
 */
export interface FilesScanningOptions {
  /** TCP socket timeout for clamd INSTREAM scans, in ms. Default: 30_000 (30s). */
  readonly socketTimeoutMs: number;
}

export const DEFAULT_FILES_SCANNING_OPTIONS: FilesScanningOptions = {
  socketTimeoutMs: 30_000,
};

/**
 * Outbound options for the Files service.
 *
 * Controls timeouts on outbound gRPC calls to other services (callbacks for
 * `OnFileProcessed` / `CanAccess`) and to the SignalR Gateway (`PushFileUpdate`).
 * Tight enough to fail fast under partial outages, generous enough to absorb
 * normal cross-service latency.
 */
export interface FilesOutboundOptions {
  /** Per-call deadline applied to outbound gRPC calls, in ms. Default: 10_000 (10s). */
  readonly grpcTimeoutMs: number;
}

export const DEFAULT_FILES_OUTBOUND_OPTIONS: FilesOutboundOptions = {
  grpcTimeoutMs: 10_000,
};
