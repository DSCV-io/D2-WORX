import type { NodePgDatabase } from "drizzle-orm/node-postgres";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { PingDbInput as I, PingDbOutput as O, IPingDbHandler } from "@d2/files-app";
import { file } from "../../schema/tables.js";

export class PingDb extends BaseHandler<I, O> implements IPingDbHandler {
  private readonly db: NodePgDatabase;

  constructor(db: NodePgDatabase, context: IHandlerContext) {
    super(context);
    this.db = db;
  }

  protected async executeAsync(_input: I): Promise<D2Result<O | undefined>> {
    const start = performance.now();
    try {
      await this.db.select({ id: file.id }).from(file).limit(0);
      const latencyMs = Math.round(performance.now() - start);
      return D2Result.ok({ data: { healthy: true, latencyMs } });
    } catch (err: unknown) {
      const latencyMs = Math.round(performance.now() - start);
      const error = err instanceof Error ? err.message : String(err);
      return D2Result.ok({ data: { healthy: false, latencyMs, error } });
    }
  }
}
