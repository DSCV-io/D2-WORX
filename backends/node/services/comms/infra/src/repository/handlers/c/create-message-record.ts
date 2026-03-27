import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  CreateMessageRecordInput as I,
  CreateMessageRecordOutput as O,
  ICreateMessageRecordHandler,
} from "@d2/comms-app";
import { message } from "../../schema/tables.js";

export class CreateMessageRecord extends BaseHandler<I, O> implements ICreateMessageRecordHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const m = input.message;
    await this.db.insert(message).values({
      id: m.id,
      threadId: m.threadId,
      parentMessageId: m.parentMessageId,
      senderUserId: m.senderUserId,
      senderContactId: m.senderContactId,
      senderService: m.senderService,
      title: m.title,
      content: m.content,
      plainTextContent: m.plainTextContent,
      contentFormat: m.contentFormat,
      channels: m.channels,
      urgency: m.urgency,
      relatedEntityId: m.relatedEntityId,
      relatedEntityType: m.relatedEntityType,
      metadata: m.metadata,
      editedAt: m.editedAt,
      deletedAt: m.deletedAt,
      createdAt: m.createdAt,
      updatedAt: m.updatedAt,
    });

    return D2Result.ok({ data: {} });
  }
}
