import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import { updateOrgContact, GEO_CONTEXT_KEYS, type UpdateOrgContactInput } from "@d2/auth-domain";
import type { ContactDTO, ContactToCreateDTO } from "@d2/protos";
import { contactInputSchema, type Complex, type Queries as GeoQueries } from "@d2/geo-client";
import type {
  IFindOrgContactByIdHandler,
  IUpdateOrgContactRecordHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";
import { runCrossServiceUpdate } from "../u/cross-service-update.js";

type Input = Commands.UpdateOrgContactHandlerInput;
type Output = Commands.UpdateOrgContactOutput;

const schema = z.object({
  id: zodGuid,
  organizationId: zodGuid,
  updates: z
    .object({
      label: z.string().max(100).optional(),
      isPrimary: z.boolean().optional(),
      ietfBcp47Tag: z.string().max(35).optional(),
      contact: contactInputSchema.optional(),
    })
    .refine(
      (u) => u.label !== undefined || u.isPrimary !== undefined || u.contact !== undefined,
      "At least one field (label, isPrimary, or contact) must be provided.",
    ),
});

/**
 * Updates an existing org contact junction record.
 *
 * Two modes:
 * 1. **Metadata-only** (label and/or isPrimary) — updates junction fields in place.
 * 2. **Contact replacement** (contact details provided) — uses SAGA pattern:
 *    Geo update first → auth metadata update → if auth fails, Geo rolled back to
 *    the original contact. On rollback failure → logger.fatal() (CRITICAL).
 */
export class UpdateOrgContactHandler
  extends BaseHandler<Input, Output>
  implements Commands.IUpdateOrgContactHandler
{
  private readonly findById: IFindOrgContactByIdHandler;
  private readonly updateRecord: IUpdateOrgContactRecordHandler;
  private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;
  private readonly updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler;

  constructor(
    findById: IFindOrgContactByIdHandler,
    updateRecord: IUpdateOrgContactRecordHandler,
    context: IHandlerContext,
    getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
    updateContactsByExtKeys: Complex.IUpdateContactsByExtKeysHandler,
  ) {
    super(context);
    this.findById = findById;
    this.updateRecord = updateRecord;
    this.getContactsByExtKeys = getContactsByExtKeys;
    this.updateContactsByExtKeys = updateContactsByExtKeys;
  }

  override get redaction() {
    return Commands.UPDATE_ORG_CONTACT_REDACTION;
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

    // Compute the metadata patch up-front so both branches can apply it.
    const metadataUpdates: UpdateOrgContactInput = {};
    if (input.updates.label !== undefined) {
      (metadataUpdates as Record<string, unknown>).label = input.updates.label;
    }
    if (input.updates.isPrimary !== undefined) {
      (metadataUpdates as Record<string, unknown>).isPrimary = input.updates.isPrimary;
    }
    const updated = updateOrgContact(existing, metadataUpdates);

    let newGeoContact: ContactDTO | undefined;

    // -- Mode 2: contact replacement (SAGA — Geo + Auth atomic) --
    if (input.updates.contact || input.updates.ietfBcp47Tag !== undefined) {
      const extKey = { contextKey: GEO_CONTEXT_KEYS.ORG_CONTACT, relatedEntityId: existing.id };
      const existingResult = await this.getContactsByExtKeys.handleAsync({ keys: [extKey] });
      if (!existingResult.success) {
        this.context.logger.error("Failed to fetch existing Geo contact for merge", {
          orgContactId: existing.id,
          errorCode: existingResult.errorCode,
        });
        return D2Result.serviceUnavailable({
          messages: [TK.common.errors.SERVICE_UNAVAILABLE],
        });
      }
      const mapKey = `${extKey.contextKey}:${extKey.relatedEntityId}`;
      const existingGeoContact = existingResult.data?.data.get(mapKey)?.[0];
      const { id: _, ...existingFields } = existingGeoContact ?? {};

      // Snapshot — pre-update contact (rollback target).
      const oldContact: ContactToCreateDTO = {
        ...existingFields,
        createdAt: new Date(),
        contextKey: extKey.contextKey,
        relatedEntityId: extKey.relatedEntityId,
      };

      // Target — apply only the fields the caller specified.
      const newContact: ContactToCreateDTO = {
        ...existingFields,
        createdAt: new Date(),
        contextKey: extKey.contextKey,
        relatedEntityId: extKey.relatedEntityId,
        ...(input.updates.ietfBcp47Tag !== undefined && {
          ietfBcp47Tag: input.updates.ietfBcp47Tag,
        }),
        ...(input.updates.contact?.contactMethods && {
          contactMethods: input.updates.contact.contactMethods,
        }),
        ...(input.updates.contact?.personalDetails && {
          personalDetails: input.updates.contact.personalDetails,
        }),
        ...(input.updates.contact?.professionalDetails && {
          professionalDetails: input.updates.contact.professionalDetails,
        }),
        ...(input.updates.contact?.location && { location: input.updates.contact.location }),
      };

      const sagaResult = await runCrossServiceUpdate({
        oldContact,
        newContact,
        updateContactsByExtKeys: this.updateContactsByExtKeys,
        operationLabel: "org_contact",
        context: this.context,
        onGeoSuccess: (geoResult) => {
          // Capture new Geo contact for the response.
          newGeoContact = geoResult.data?.replacements[0]?.newContact;
        },
        authUpdate: async () => {
          // Defensive: if Geo "succeeded" but returned no replacements, treat
          // as a service failure. Auth update will fail → saga rolls back Geo
          // (no-op since Geo didn't actually change anything).
          if (!newGeoContact) {
            return D2Result.serviceUnavailable({
              messages: [TK.common.errors.SERVICE_UNAVAILABLE],
            });
          }
          return this.updateRecord.handleAsync({ contact: updated });
        },
      });
      if (!sagaResult.success) return D2Result.bubbleFail(sagaResult);
    } else {
      // -- Mode 1: metadata-only — no Geo update, no saga needed --
      const updateResult = await this.updateRecord.handleAsync({ contact: updated });
      if (!updateResult.success) return D2Result.bubbleFail(updateResult);
    }

    return D2Result.ok({
      data: { contact: updated, geoContact: newGeoContact },
    });
  }
}

export type {
  UpdateOrgContactHandlerInput,
  UpdateOrgContactOutput,
} from "../../../../interfaces/cqrs/handlers/c/update-org-contact.js";
