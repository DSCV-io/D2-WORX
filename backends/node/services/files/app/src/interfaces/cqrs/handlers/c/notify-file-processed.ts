import type { IHandler } from "@d2/handler";

export interface NotifyFileProcessedInput {
  readonly address: string;
  readonly fileId: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly status: "ready" | "rejected";
  readonly variants?: readonly string[];
}

export interface NotifyFileProcessedOutput {
  readonly success: boolean;
}

/** gRPC OnFileProcessed callback — notifies the owning service that processing completed. */
export interface INotifyFileProcessedHandler extends IHandler<
  NotifyFileProcessedInput,
  NotifyFileProcessedOutput
> {}
