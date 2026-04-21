import type { IHandler, RedactionSpec } from "@d2/handler";

export interface UpdateOrgLogoInput {
  readonly orgId: string;
  readonly logo: string | null;
}

export interface UpdateOrgLogoOutput {}

/** `logo` is a presigned URL — may embed identifiers; redact from logs. */
export const UPDATE_ORG_LOGO_REDACTION: RedactionSpec = {
  inputFields: ["logo"],
};

export interface IUpdateOrgLogoHandler extends IHandler<UpdateOrgLogoInput, UpdateOrgLogoOutput> {
  readonly redaction: RedactionSpec;
}
