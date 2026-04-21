import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateUserImageInput {
  readonly userId: string;
  readonly image: string | null;
}

export interface UpdateUserImageOutput {}

/** `image` is a presigned/avatar URL — treat as PII (may embed identifiers). */
export const UPDATE_USER_IMAGE_REDACTION: RedactionSpec = {
  inputFields: ["image"],
};

export interface IUpdateUserImageHandler extends IHandler<UpdateUserImageInput, UpdateUserImageOutput> {
  readonly redaction: RedactionSpec;
}
