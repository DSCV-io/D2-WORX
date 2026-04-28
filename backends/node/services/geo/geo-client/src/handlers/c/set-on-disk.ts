import { mkdir, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { GeoRefData as GeoRefDataCodec } from "@d2/protos";
import { GEO_REF_DATA_FILE_NAME } from "@d2/utilities";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { Commands } from "../../interfaces/index.js";

type Input = Commands.SetOnDiskInput;
type Output = Commands.SetOnDiskOutput;

/**
 * Handler for persisting georeference data to disk as protobuf binary.
 * Mirrors D2.Geo.Client.CQRS.Handlers.C.SetOnDisk in .NET.
 */
export class SetOnDisk extends BaseHandler<Input, Output> implements Commands.ISetOnDiskHandler {
  override get redaction() {
    return Commands.SET_ON_DISK_REDACTION;
  }

  private readonly filePath: string;

  constructor(options: GeoClientOptions, context: IHandlerContext) {
    super(context);
    this.filePath = join(options.dataDir, GEO_REF_DATA_FILE_NAME);
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    try {
      const bytes = GeoRefDataCodec.encode(input.data).finish();
      await mkdir(join(this.filePath, ".."), { recursive: true });
      await writeFile(this.filePath, bytes);
      return D2Result.ok({ data: {} });
    } catch (err: unknown) {
      this.context.logger.error(
        `IOException occurred while writing georeference data to disk. TraceId: ${this.traceId}`,
        { error: err instanceof Error ? err.message : String(err) },
      );
      return D2Result.unhandledException({
        messages: ["Unable to write to disk."],
      });
    }
  }
}

export type { SetOnDiskInput, SetOnDiskOutput } from "../../interfaces/c/set-on-disk.js";
