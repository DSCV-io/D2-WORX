import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid, zodNonEmptyString } from "@d2/handler";
import { D2Result } from "@d2/result";
import { generateUuidV7 } from "@d2/utilities";
import { createOrgContact, GEO_CONTEXT_KEYS } from "@d2/auth-domain";
import type { ContactToCreateDTO } from "@d2/protos";
import { contactInputSchema, type Commands as GeoCommands } from "@d2/geo-client";
import type {
  ICreateOrgContactRecordHandler,
  IDeleteOrgContactRecordHandler,
} from "../../../../interfaces/repository/handlers/index.js";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.CreateOrgContactInput;
type Output = Commands.CreateOrgContactOutput;

const schema = z.object({
  organizationId: zodGuid,
  label: zodNonEmptyString(100),
  isPrimary: z.boolean().optional(),
  ietfBcp47Tag: z.string().max(35).optional(),
  contact: contactInputSchema,
});

/**
 * Creates an org contact: first creates the junction record, then creates
 * the Geo contact via gRPC using the junction ID as relatedEntityId.
 *
 * The caller provides full contact details (not a contactId). This handler
 * pre-generates the org_contact ID, creates the junction, then creates the
 * Geo contact with contextKey="auth_org_contact" and relatedEntityId=orgContact.id.
 * If Geo creation fails, the junction is rolled back (deleted).
 */
export class CreateOrgContact
  extends BaseHandler<Input, Output>
  implements Commands.ICreateOrgContactHandler
{
  private readonly createRecord: ICreateOrgContactRecordHandler;
  private readonly deleteRecord: IDeleteOrgContactRecordHandler;
  private readonly createContacts: GeoCommands.ICreateContactsHandler;

  override get redaction() {
    return Commands.CREATE_ORG_CONTACT_REDACTION;
  }

  constructor(
    createRecord: ICreateOrgContactRecordHandler,
    deleteRecord: IDeleteOrgContactRecordHandler,
    context: IHandlerContext,
    createContacts: GeoCommands.ICreateContactsHandler,
  ) {
    super(context);
    this.createRecord = createRecord;
    this.deleteRecord = deleteRecord;
    this.createContacts = createContacts;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    // Pre-generate org_contact ID — this becomes the relatedEntityId for Geo
    const orgContactId = generateUuidV7();

    // Create junction record first (no contactId column)
    const contact = createOrgContact({
      id: orgContactId,
      organizationId: input.organizationId,
      label: input.label,
      isPrimary: input.isPrimary,
    });

    const createResult = await this.createRecord.handleAsync({ contact });
    if (!createResult.success) return D2Result.bubbleFail(createResult);

    // Build ContactToCreateDTO using junction ID as relatedEntityId
    const contactToCreate = {
      createdAt: new Date(),
      contextKey: GEO_CONTEXT_KEYS.ORG_CONTACT,
      relatedEntityId: orgContactId,
      ietfBcp47Tag: input.ietfBcp47Tag ?? undefined,
      contactMethods: input.contact.contactMethods ?? undefined,
      personalDetails: input.contact.personalDetails ?? undefined,
      professionalDetails: input.contact.professionalDetails ?? undefined,
      location: input.contact.location ?? undefined,
    } as ContactToCreateDTO;

    const geoResult = await this.createContacts.handleAsync({ contacts: [contactToCreate] });
    if (!geoResult.success || !geoResult.data) {
      // Rollback: delete the junction since Geo contact creation failed
      try {
        await this.deleteRecord.handleAsync({ id: orgContactId });
      } catch (err: unknown) {
        this.context.logger.error("CreateOrgContact: rollback failed", {
          error: err instanceof Error ? err.message : String(err),
        });
      }
      return D2Result.bubbleFail(geoResult);
    }

    const geoContact = geoResult.data.data[0];
    if (!geoContact) {
      try {
        await this.deleteRecord.handleAsync({ id: orgContactId });
      } catch (err: unknown) {
        this.context.logger.error("CreateOrgContact: rollback failed", {
          error: err instanceof Error ? err.message : String(err),
        });
      }
      return D2Result.bubbleFail(geoResult);
    }

    return D2Result.ok({ data: { contact, geoContact } });
  }
}

export type {
  ContactInput,
  CreateOrgContactInput,
  CreateOrgContactOutput,
} from "../../../../interfaces/cqrs/handlers/c/create-org-contact.js";
