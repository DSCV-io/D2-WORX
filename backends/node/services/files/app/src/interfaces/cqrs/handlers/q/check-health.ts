import type { IHandler } from "@d2/handler";

export interface CheckHealthInput {}

export interface ComponentHealth {
  status: string;
  latencyMs?: number;
  error?: string;
}

export interface CheckHealthOutput {
  status: string;
  components: Record<string, ComponentHealth>;
}

export interface ICheckHealthHandler extends IHandler<CheckHealthInput, CheckHealthOutput> {}
