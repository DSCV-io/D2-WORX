import type { IHandler, RedactionSpec } from "@d2/handler";
import type { OrgContact } from "@d2/auth-domain";

export interface FindOrgContactByIdInput {
  readonly id: string;
}

export interface FindOrgContactByIdOutput {
  readonly contact: OrgContact;
}

/** Output `contact.label` is user-supplied free text (PII) — suppress full output. */
export const FIND_ORG_CONTACT_BY_ID_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

export interface IFindOrgContactByIdHandler
  extends IHandler<FindOrgContactByIdInput, FindOrgContactByIdOutput> {
  readonly redaction: RedactionSpec;
}
