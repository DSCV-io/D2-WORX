import type { IHandler } from "@d2/handler";

export interface HandleFileProcessedInput {
  readonly fileId: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly status: "ready" | "rejected";
  readonly variants?: readonly string[];
}

export interface HandleFileProcessedOutput {
  readonly success: boolean;
}

export interface IHandleFileProcessedHandler extends IHandler<
  HandleFileProcessedInput,
  HandleFileProcessedOutput
> {}
