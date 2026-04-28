import type { IHandler } from "@d2/handler";

export interface PublishFileForProcessingInput {
  readonly fileId: string;
}

export interface PublishFileForProcessingOutput {}

/** Publishes a file ID to the processing queue after intake transitions it to "processing". */
export interface IPublishFileForProcessingHandler extends IHandler<
  PublishFileForProcessingInput,
  PublishFileForProcessingOutput
> {}
