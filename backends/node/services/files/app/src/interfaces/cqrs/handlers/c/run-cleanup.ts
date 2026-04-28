import type { IHandler } from "@d2/handler";

export interface RunCleanupInput {}

export interface RunCleanupOutput {
  readonly lockAcquired: boolean;
  readonly pendingCleaned: number;
  readonly processingCleaned: number;
  readonly rejectedCleaned: number;
  readonly durationMs: number;
}

export interface IRunCleanupHandler extends IHandler<RunCleanupInput, RunCleanupOutput> {}
