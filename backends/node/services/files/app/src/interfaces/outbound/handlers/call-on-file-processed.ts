import type { IHandler, RedactionSpec } from "@d2/handler";

export interface CallOnFileProcessedInput {
  readonly address: string;
  readonly fileId: string;
  readonly contextKey: string;
  readonly relatedEntityId: string;
  readonly status: "ready" | "rejected";
  readonly variants?: readonly string[];
}

export interface CallOnFileProcessedOutput {
  readonly success: boolean;
}

/** `address` is an internal service endpoint — redact from logs. */
export const CALL_ON_FILE_PROCESSED_REDACTION: RedactionSpec = {
  inputFields: ["address"],
};

/** gRPC OnFileProcessed call — notifies the owning service that processing completed. */
export interface ICallOnFileProcessed
  extends IHandler<CallOnFileProcessedInput, CallOnFileProcessedOutput> {
  readonly redaction: RedactionSpec;
}
