import type { IHandler, RedactionSpec } from "@d2/handler";

export interface CheckSoleOwnerOrgsInput {
  readonly userId: string;
}

export interface CheckSoleOwnerOrgsOutput {
  /**
   * Org IDs where the input user is the only `owner`-role member. Empty
   * means the user can be deleted without disrupting any org.
   */
  readonly soleOwnerOrgIds: string[];
}

export const CHECK_SOLE_OWNER_ORGS_REDACTION: RedactionSpec = {};

export type ICheckSoleOwnerOrgsHandler = IHandler<
  CheckSoleOwnerOrgsInput,
  CheckSoleOwnerOrgsOutput
>;
