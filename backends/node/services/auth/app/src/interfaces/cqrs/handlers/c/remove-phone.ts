import type { IHandler, RedactionSpec } from "@d2/handler";

export interface RemovePhoneInput {
  readonly userId: string;
  readonly currentPassword: string;
}

export interface RemovePhoneOutput {}

export const REMOVE_PHONE_REDACTION: RedactionSpec = {
  inputFields: ["currentPassword"],
  suppressOutput: false,
};

export interface IRemovePhoneHandler extends IHandler<RemovePhoneInput, RemovePhoneOutput> {
  readonly redaction: RedactionSpec;
}
