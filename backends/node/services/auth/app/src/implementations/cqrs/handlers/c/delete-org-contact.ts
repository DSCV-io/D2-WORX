import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { Commands as GeoCommands } from "@d2/geo-client";
import type {
  IFindOrgContactByIdHandler,
  IDeleteOrgContactRecordHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.DeleteOrgContactInput;
type Output = Commands.DeleteOrgContactOutput;

const schema = z.object({
  id: zodGuid,
  organizationId: zodGuid,
});

/**
 * Deletes an org contact junction record and its associated Geo contact.
 *
 * Validates input, checks existence, verifies IDOR (contact belongs to org),
 * deletes the Geo contact by ext key (best-effort), then deletes the junction.
 */
export class DeleteOrgContact
  extends BaseHandler<Input, Output>
  implements Commands.IDeleteOrgContactHandler
{
  private readonly findById: IFindOrgContactByIdHandler;
  private readonly deleteRecord: IDeleteOrgContactRecordHandler;
  private readonly deleteContactsByExtKeys: GeoCommands.IDeleteContactsByExtKeysHandler;

  constructor(
    findById: IFindOrgContactByIdHandler,
    deleteRecord: IDeleteOrgContactRecordHandler,
    context: IHandlerContext,
    deleteContactsByExtKeys: GeoCommands.IDeleteContactsByExtKeysHandler,
  ) {
    super(context);
    this.findById = findById;
    this.deleteRecord = deleteRecord;
    this.deleteContactsByExtKeys = deleteContactsByExtKeys;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.findById.handleAsync({ id: input.id });
    if (!findResult.success || !findResult.data) {
      return D2Result.notFound();
    }

    const existing = findResult.data.contact;

    // IDOR check: contact must belong to the caller's active org
    if (existing.organizationId !== input.organizationId) {
      return D2Result.forbidden({
        messages: [TK.auth.errors.ORG_CONTACT_ORG_MISMATCH],
      });
    }

    // Best-effort: delete the associated Geo contact by ext key.
    // If this fails, Geo's background job handles orphan cleanup.
    try {
      await this.deleteContactsByExtKeys.handleAsync({
        keys: [{ contextKey: GEO_CONTEXT_KEYS.ORG_CONTACT, relatedEntityId: existing.id }],
      });
    } catch (err: unknown) {
      // Swallow — Geo cleanup is non-critical
      this.context.logger.warn("DeleteOrgContact: Geo contact cleanup failed (non-critical)", {
        error: err instanceof Error ? err.message : String(err),
      });
    }

    // Delete the junction record
    const deleteResult = await this.deleteRecord.handleAsync({ id: input.id });
    if (!deleteResult.success) return D2Result.bubbleFail(deleteResult);

    return D2Result.ok({ data: {} });
  }
}

export type {
  DeleteOrgContactInput,
  DeleteOrgContactOutput,
} from "../../../../interfaces/cqrs/handlers/c/delete-org-contact.js";
