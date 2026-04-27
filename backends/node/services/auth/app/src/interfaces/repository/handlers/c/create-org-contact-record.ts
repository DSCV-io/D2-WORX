import type { IHandler, RedactionSpec } from "@d2/handler";
import type { OrgContact } from "@d2/auth-domain";

export interface CreateOrgContactRecordInput {
  readonly contact: OrgContact;
}

export interface CreateOrgContactRecordOutput {}

/** Input `contact.label` is user-supplied free text (PII) — suppress full input. */
export const CREATE_ORG_CONTACT_RECORD_REDACTION: RedactionSpec = {
  suppressInput: true,
};

export interface ICreateOrgContactRecordHandler extends IHandler<
  CreateOrgContactRecordInput,
  CreateOrgContactRecordOutput
> {
  readonly redaction: RedactionSpec;
}
