import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { createEmulationConsent, canEmulate, ORG_TYPES } from "@d2/auth-domain";
import type {
  ICreateEmulationConsentRecordHandler,
  IFindActiveConsentByUserIdAndOrgHandler,
  ICheckOrgExistsHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.CreateEmulationConsentInput;
type Output = Commands.CreateEmulationConsentOutput;

/** Maximum consent duration: 30 days in milliseconds. */
const MAX_CONSENT_DURATION_MS = 30 * 24 * 60 * 60 * 1000;

const schema = z.object({
  userId: zodGuid,
  grantedToOrgId: zodGuid,
  activeOrgType: z.enum(ORG_TYPES),
  expiresAt: z
    .date()
    .refine((d) => d.getTime() > Date.now(), "expiresAt must be in the future.")
    .refine(
      (d) => d.getTime() - Date.now() <= MAX_CONSENT_DURATION_MS,
      "Consent duration cannot exceed 30 days.",
    ),
});

/**
 * Creates an emulation consent record.
 *
 * Validates input, checks org type can emulate, verifies the target org
 * exists, and prevents duplicate active consents for the same user+org.
 */
export class CreateEmulationConsent
  extends BaseHandler<Input, Output>
  implements Commands.ICreateEmulationConsentHandler
{
  private readonly createRecord: ICreateEmulationConsentRecordHandler;
  private readonly findActiveByUserIdAndOrg: IFindActiveConsentByUserIdAndOrgHandler;
  private readonly checkOrgExists: ICheckOrgExistsHandler;

  constructor(
    createRecord: ICreateEmulationConsentRecordHandler,
    findActiveByUserIdAndOrg: IFindActiveConsentByUserIdAndOrgHandler,
    context: IHandlerContext,
    checkOrgExists: ICheckOrgExistsHandler,
  ) {
    super(context);
    this.createRecord = createRecord;
    this.findActiveByUserIdAndOrg = findActiveByUserIdAndOrg;
    this.checkOrgExists = checkOrgExists;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    if (!canEmulate(input.activeOrgType)) {
      return D2Result.forbidden({
        messages: [TK.auth.errors.EMULATION_ORG_TYPE_NOT_ALLOWED],
      });
    }

    // Verify target org exists
    const orgResult = await this.checkOrgExists.handleAsync({ orgId: input.grantedToOrgId });
    if (!orgResult.success) return D2Result.bubbleFail(orgResult);
    if (!orgResult.data?.exists) {
      return D2Result.notFound();
    }

    // Prevent duplicate active consents for same user+org
    const findResult = await this.findActiveByUserIdAndOrg.handleAsync({
      userId: input.userId,
      grantedToOrgId: input.grantedToOrgId,
    });
    if (findResult.success && findResult.data?.consent) {
      return D2Result.conflict({
        messages: [TK.auth.errors.EMULATION_CONSENT_ALREADY_EXISTS],
      });
    }

    const consent = createEmulationConsent({
      userId: input.userId,
      grantedToOrgId: input.grantedToOrgId,
      expiresAt: input.expiresAt,
    });

    const createResult = await this.createRecord.handleAsync({ consent });
    if (!createResult.success) return D2Result.bubbleFail(createResult);

    return D2Result.ok({ data: { consent } });
  }
}

export type {
  CreateEmulationConsentInput,
  CreateEmulationConsentOutput,
} from "../../../../interfaces/cqrs/handlers/c/create-emulation-consent.js";
