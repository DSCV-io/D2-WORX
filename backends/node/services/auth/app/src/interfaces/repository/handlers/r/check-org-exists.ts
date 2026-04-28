import type { IHandler } from "@d2/handler";

export interface CheckOrgExistsInput {
  readonly orgId: string;
}

export interface CheckOrgExistsOutput {
  readonly exists: boolean;
}

export type ICheckOrgExistsHandler = IHandler<CheckOrgExistsInput, CheckOrgExistsOutput>;
