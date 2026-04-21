import type { IHandler, RedactionSpec } from "@d2/handler";
import type { OrgContact } from "@d2/auth-domain";

export interface UpdateOrgContactRecordInput {
  readonly contact: OrgContact;
}

export interface UpdateOrgContactRecordOutput {}

/** Input `contact.label` is user-supplied free text (PII) — suppress full input. */
export const UPDATE_ORG_CONTACT_RECORD_REDACTION: RedactionSpec = {
  suppressInput: true,
};

export interface IUpdateOrgContactRecordHandler
  extends IHandler<UpdateOrgContactRecordInput, UpdateOrgContactRecordOutput> {
  readonly redaction: RedactionSpec;
}
