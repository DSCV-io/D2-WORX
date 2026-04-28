import { z } from "zod";
import { BaseHandler, type IHandlerContext, zodGuid } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { ContactDTO } from "@d2/protos";
import { GEO_CONTEXT_KEYS, type OrgContact } from "@d2/auth-domain";
import type { Queries as GeoQueries } from "@d2/geo-client";
import type { IFindOrgContactsByOrgIdHandler } from "../../../../interfaces/repository/handlers/index.js";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.GetOrgContactsInput;
type Output = Queries.GetOrgContactsOutput;
type HydratedOrgContact = Queries.HydratedOrgContact;

const schema = z.object({
  organizationId: zodGuid,
  limit: z.number().int().min(1).max(100).optional(),
  offset: z.number().int().min(0).optional(),
});

/**
 * Retrieves contacts associated with an organization, hydrated with full
 * Geo contact data via ext keys.
 *
 * Loads junction records from the repo, batch-fetches Geo contact data
 * via GetContactsByExtKeys (using contextKey="auth_org_contact", relatedEntityId=junction.id),
 * then joins the results. Orphaned junctions (where the Geo contact no longer
 * exists) are returned with geoContact: undefined.
 */
export class GetOrgContacts
  extends BaseHandler<Input, Output>
  implements Queries.IGetOrgContactsHandler
{
  private readonly findByOrgId: IFindOrgContactsByOrgIdHandler;
  private readonly getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler;

  constructor(
    findByOrgId: IFindOrgContactsByOrgIdHandler,
    context: IHandlerContext,
    getContactsByExtKeys: GeoQueries.IGetContactsByExtKeysHandler,
  ) {
    super(context);
    this.findByOrgId = findByOrgId;
    this.getContactsByExtKeys = getContactsByExtKeys;
  }

  override get redaction() {
    return Queries.GET_ORG_CONTACTS_REDACTION;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const findResult = await this.findByOrgId.handleAsync({
      organizationId: input.organizationId,
      limit: input.limit ?? 50,
      offset: input.offset ?? 0,
    });

    if (!findResult.success) return D2Result.bubbleFail(findResult);
    const junctions: OrgContact[] = findResult.data?.contacts ?? [];

    if (junctions.length === 0) {
      return D2Result.ok({ data: { contacts: [] } });
    }

    // Batch fetch Geo contacts using ext keys
    const keys = junctions.map((j) => ({
      contextKey: GEO_CONTEXT_KEYS.ORG_CONTACT,
      relatedEntityId: j.id,
    }));

    let geoContactMap: Map<string, ContactDTO[]> = new Map();

    const geoResult = await this.getContactsByExtKeys.handleAsync({ keys });
    if (geoResult.success && geoResult.data) {
      geoContactMap = geoResult.data.data;
    }

    // Join: each junction gets its matching Geo contact (or undefined if orphaned)
    const contacts: HydratedOrgContact[] = junctions.map((j) => ({
      id: j.id,
      organizationId: j.organizationId,
      label: j.label,
      isPrimary: j.isPrimary,
      createdAt: j.createdAt,
      updatedAt: j.updatedAt,
      geoContact: geoContactMap.get(`${GEO_CONTEXT_KEYS.ORG_CONTACT}:${j.id}`)?.[0],
    }));

    return D2Result.ok({ data: { contacts } });
  }
}

export type {
  HydratedOrgContact,
  GetOrgContactsInput,
  GetOrgContactsOutput,
} from "../../../../interfaces/cqrs/handlers/q/get-org-contacts.js";
