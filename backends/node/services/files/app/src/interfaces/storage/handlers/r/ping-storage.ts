import type { IHandler } from "@d2/handler";

export interface PingStorageInput {}

export interface PingStorageOutput {
  readonly healthy: boolean;
  readonly latencyMs: number;
  readonly error?: string;
}

/** Verifies object storage connectivity. */
export type IPingStorage = IHandler<PingStorageInput, PingStorageOutput>;
