import { eq } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  FindMessageByIdInput as I,
  FindMessageByIdOutput as O,
  IFindMessageByIdHandler,
} from "@d2/comms-app";
import type { Message, ContentFormat, Urgency } from "@d2/comms-domain";
import { message } from "../../schema/tables.js";
import type { MessageRow } from "../../schema/types.js";

export class FindMessageById extends BaseHandler<I, O> implements IFindMessageByIdHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return { suppressOutput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const rows = await this.db.select().from(message).where(eq(message.id, input.id)).limit(1);

    const row = rows[0];
    if (!row) {
      return D2Result.notFound();
    }

    return D2Result.ok({ data: { message: toMessage(row) } });
  }
}

function toMessage(row: MessageRow): Message {
  return {
    id: row.id,
    threadId: row.threadId ?? undefined,
    parentMessageId: row.parentMessageId ?? undefined,
    senderUserId: row.senderUserId ?? undefined,
    senderContactId: row.senderContactId ?? undefined,
    senderService: row.senderService ?? undefined,
    title: row.title ?? undefined,
    content: row.content,
    plainTextContent: row.plainTextContent,
    contentFormat: row.contentFormat as ContentFormat,
    channels: (row.channels ?? []) as ("email" | "sms")[],
    urgency: row.urgency as Urgency,
    relatedEntityId: row.relatedEntityId ?? undefined,
    relatedEntityType: row.relatedEntityType ?? undefined,
    metadata: (row.metadata as Record<string, unknown>) ?? undefined,
    editedAt: row.editedAt ?? undefined,
    deletedAt: row.deletedAt ?? undefined,
    createdAt: row.createdAt,
    updatedAt: row.updatedAt,
  };
}
