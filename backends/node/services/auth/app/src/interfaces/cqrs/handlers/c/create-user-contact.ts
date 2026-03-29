import type { IHandler, RedactionSpec } from "@d2/handler";
import type { ContactDTO } from "@d2/protos";

export interface CreateUserContactInput {
  readonly userId: string;
  readonly email: string;
  readonly name: string;
  readonly locale: string;
  readonly timezone?: string;
}

export interface CreateUserContactOutput {
  readonly contact: ContactDTO;
}

/** Recommended redaction for CreateUserContact handlers. */
export const CREATE_USER_CONTACT_REDACTION: RedactionSpec = {
  suppressInput: true,
  suppressOutput: true,
};

/** Handler for creating user contacts during sign-up. Requires redaction (I/O contains PII). */
export interface ICreateUserContactHandler extends IHandler<
  CreateUserContactInput,
  CreateUserContactOutput
> {
  readonly redaction: RedactionSpec;
}
