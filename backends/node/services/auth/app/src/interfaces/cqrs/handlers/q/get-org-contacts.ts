import type { IHandler, RedactionSpec } from "@d2/handler";
import type { ContactDTO } from "@d2/protos";

/** A junction record hydrated with full Geo contact data. */
export interface HydratedOrgContact {
  readonly id: string;
  readonly organizationId: string;
  readonly label: string;
  readonly isPrimary: boolean;
  readonly createdAt: Date;
  readonly updatedAt: Date;
  /** Full Geo contact data. Undefined if the Geo contact was not found (orphaned). */
  readonly geoContact?: ContactDTO;
}

export interface GetOrgContactsInput {
  readonly organizationId: string;
  readonly limit?: number;
  readonly offset?: number;
}

export interface GetOrgContactsOutput {
  contacts: HydratedOrgContact[];
}

/** Recommended redaction for GetOrgContacts handlers. */
export const GET_ORG_CONTACTS_REDACTION: RedactionSpec = {
  suppressOutput: true,
};

/** Handler for retrieving org contacts with Geo data. Requires redaction (output contains PII). */
export interface IGetOrgContactsHandler extends IHandler<
  GetOrgContactsInput,
  GetOrgContactsOutput
> {
  readonly redaction: RedactionSpec;
}
