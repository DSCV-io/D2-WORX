import type { IHandler, RedactionSpec } from "@d2/handler";

/**
 * Sets or clears the organization's logo (file id / URL).
 *
 * Pattern: explicit `clear: boolean` separates "set to a value" from "remove
 * the value entirely" — avoids the `null`-as-data ambiguity. When `clear` is
 * `true`, `logo` is ignored and the DB column is set to `NULL`. When `clear`
 * is `false`, `logo` MUST be a defined non-empty string and is written
 * verbatim. Chosen over splitting into `SetOrgLogo` / `ClearOrgLogo` to keep
 * one DI key + one repo handler for what is fundamentally one column mutation.
 */
export interface UpdateOrgLogoInput {
  readonly orgId: string;
  /** New logo value. Required when `clear` is `false`; ignored when `clear` is `true`. */
  readonly logo?: string;
  /** `true` to set the DB column to NULL (remove logo); `false` to write `logo`. */
  readonly clear: boolean;
}

export interface UpdateOrgLogoOutput {}

/** `logo` is a presigned URL — may embed identifiers; redact from logs. */
export const UPDATE_ORG_LOGO_REDACTION: RedactionSpec = {
  inputFields: ["logo"],
};

export interface IUpdateOrgLogoHandler extends IHandler<UpdateOrgLogoInput, UpdateOrgLogoOutput> {
  readonly redaction: RedactionSpec;
}
