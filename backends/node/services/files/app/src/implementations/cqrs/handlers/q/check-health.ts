import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";
import { D2Result } from "@d2/result";
import type { IPingDbHandler } from "../../../../interfaces/repository/handlers/index.js";
import type { FileStorageHandlers } from "../../../../interfaces/storage/handlers/index.js";

type Input = Queries.CheckHealthInput;
type Output = Queries.CheckHealthOutput;

/**
 * Aggregates ping results from all files service dependencies into a single
 * health check response.
 */
export class CheckHealth extends BaseHandler<Input, Output> implements Queries.ICheckHealthHandler {
  private readonly pingDb: IPingDbHandler;
  private readonly storage: FileStorageHandlers;

  constructor(pingDb: IPingDbHandler, storage: FileStorageHandlers, context: IHandlerContext) {
    super(context);
    this.pingDb = pingDb;
    this.storage = storage;
  }

  protected async executeAsync(_input: Input): Promise<D2Result<Output | undefined>> {
    const components: Record<string, Queries.ComponentHealth> = {};

    const [dbResult, storageResult] = await Promise.all([
      this.pingDb.handleAsync({}),
      this.storage.ping.handleAsync({}),
    ]);

    // DB
    const dbComponent =
      dbResult.success && dbResult.data
        ? { status: "healthy" as const, latencyMs: dbResult.data.latencyMs }
        : {
            status: "unhealthy" as const,
            error: !dbResult.success ? "Ping handler failed" : "No data returned",
          };
    components["db"] = dbComponent;

    // Object storage (MinIO)
    const storageComponent =
      storageResult.success && storageResult.data
        ? { status: "healthy" as const, latencyMs: storageResult.data.latencyMs }
        : {
            status: "unhealthy" as const,
            error: !storageResult.success ? "Ping handler failed" : "No data returned",
          };
    components["storage"] = storageComponent;

    const allHealthy = Object.values(components).every((c) => c.status === "healthy");
    const status = allHealthy ? "healthy" : "degraded";

    return D2Result.ok({ data: { status, components } });
  }
}
