import type { IHandler } from "@d2/handler";

export interface PingDbInput {}

export interface PingDbOutput {
  healthy: boolean;
  latencyMs: number;
  error?: string;
}

export type IPingDbHandler = IHandler<PingDbInput, PingDbOutput>;
