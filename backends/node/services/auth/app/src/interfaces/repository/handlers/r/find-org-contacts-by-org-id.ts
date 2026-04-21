import type { IHandler, RedactionSpec } from "@d2/handler";
import type { OrgContact } from "@d2/auth-domain";

export interface FindOrgContactsByOrgIdInput {
  readonly organizationId: string;
  readonly limit?: number;
  readonly offset?: number;
}

export interface FindOrgContactsByOrgIdOutput {
  readonly contacts: OrgContact[];
}

/** Output `contacts[].label` is user-supplied free text (PII) — suppress full output. */
export const FIND_ORG_CONTACTS_BY_ORG_ID_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IFindOrgContactsByOrgIdHandler
  extends IHandler<FindOrgContactsByOrgIdInput, FindOrgContactsByOrgIdOutput> {
  readonly redaction: RedactionSpec;
}
