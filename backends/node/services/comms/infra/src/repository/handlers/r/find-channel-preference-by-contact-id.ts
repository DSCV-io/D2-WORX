import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  FindChannelPreferenceByContactIdInput as I,
  FindChannelPreferenceByContactIdOutput as O,
  IFindChannelPreferenceByContactIdHandler,
} from "@d2/comms-app";
import type { ChannelPreference } from "@d2/comms-domain";
import { channelPreference } from "../../schema/tables.js";
import type { ChannelPreferenceRow } from "../../schema/types.js";

export class FindChannelPreferenceByContactId
  extends BaseHandler<I, O>
  implements IFindChannelPreferenceByContactIdHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db
      .select()
      .from(channelPreference)
      .where(eq(channelPreference.contactId, input.contactId))
      .limit(1);

    const row = rows[0];
    return D2Result.ok({
      data: { pref: row ? toChannelPreference(row) : undefined },
    });
  }
}

export function toChannelPreference(row: ChannelPreferenceRow): ChannelPreference {
  return {
    id: row.id,
    contactId: row.contactId,
    emailEnabled: row.emailEnabled,
    smsEnabled: row.smsEnabled,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt,
  };
}
