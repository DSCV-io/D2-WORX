import type { IHandler } from "@d2/handler";

export interface IntakeFileUploadedInput {
  readonly fileId: string;
}

export interface IntakeFileUploadedOutput {}

/** Subscribes to MinIO bucket notifications — invokes IntakeFile to transition pending → processing. */
export interface IIntakeFileUploadedHandler extends IHandler<
  IntakeFileUploadedInput,
  IntakeFileUploadedOutput
> {}
