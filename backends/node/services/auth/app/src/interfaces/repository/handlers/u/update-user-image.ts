import type { IHandler, RedactionSpec } from "@d2/handler";

/**
 * Sets or clears the user's image (avatar file id / URL).
 *
 * Pattern: explicit `clear: boolean` separates "set to a value" from "remove
 * the value entirely" — avoids the `null`-as-data ambiguity. When `clear` is
 * `true`, `image` is ignored and the DB column is set to `NULL`. When `clear`
 * is `false`, `image` MUST be a defined non-empty string and is written
 * verbatim. Chosen over splitting into `SetUserImage` / `ClearUserImage` to
 * keep one DI key + one repo handler for what is fundamentally one column
 * mutation.
 */
export interface UpdateUserImageInput {
  readonly userId: string;
  /** New image value. Required when `clear` is `false`; ignored when `clear` is `true`. */
  readonly image?: string;
  /** `true` to set the DB column to NULL (remove avatar); `false` to write `image`. */
  readonly clear: boolean;
}

export interface UpdateUserImageOutput {}

/** `image` is a presigned/avatar URL — treat as PII (may embed identifiers). */
export const UPDATE_USER_IMAGE_REDACTION: RedactionSpec = {
  inputFields: ["image"],
};

export interface IUpdateUserImageHandler extends IHandler<
  UpdateUserImageInput,
  UpdateUserImageOutput
> {
  readonly redaction: RedactionSpec;
}
