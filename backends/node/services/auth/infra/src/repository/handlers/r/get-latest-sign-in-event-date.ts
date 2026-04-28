import { eq, max } from "drizzle-orm";
import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type {
  GetLatestSignInEventDateInput as I,
  GetLatestSignInEventDateOutput as O,
  IGetLatestSignInEventDateHandler,
} from "@d2/auth-app";
import { signInEvent } from "../../schema/custom-tables.js";

export class GetLatestSignInEventDate
  extends BaseHandler<I, O>
  implements IGetLatestSignInEventDateHandler
{
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    const [result] = await this.db
      .select({ latest: max(signInEvent.createdAt) })
      .from(signInEvent)
      .where(eq(signInEvent.userId, input.userId));

    let date: Date | undefined;
    if (result?.latest) {
      date = result.latest instanceof Date ? result.latest : new Date(result.latest as string);
    }

    return D2Result.ok({ data: { date } });
  }
}
