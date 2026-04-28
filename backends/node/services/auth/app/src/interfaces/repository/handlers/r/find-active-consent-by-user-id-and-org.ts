import type { IHandler } from "@d2/handler";
import type { EmulationConsent } from "@d2/auth-domain";

export interface FindActiveConsentByUserIdAndOrgInput {
  readonly userId: string;
  readonly grantedToOrgId: string;
}

export interface FindActiveConsentByUserIdAndOrgOutput {
  readonly consent?: EmulationConsent;
}

export type IFindActiveConsentByUserIdAndOrgHandler = IHandler<
  FindActiveConsentByUserIdAndOrgInput,
  FindActiveConsentByUserIdAndOrgOutput
>;
