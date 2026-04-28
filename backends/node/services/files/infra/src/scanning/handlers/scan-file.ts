import { Socket } from "node:net";
import { BaseHandler, type IHandlerContext, type RedactionSpec } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { ScanFileInput as I, ScanFileOutput as O, IScanFile } from "@d2/files-app";
import type { FilesScanningOptions } from "../../options.js";

export interface ClamdConfig {
  readonly host: string;
  readonly port: number;
}

/**
 * ClamAV virus scanner via direct TCP to clamd using the INSTREAM protocol.
 *
 * Protocol:
 * 1. Connect to clamd host:port
 * 2. Send `zINSTREAM\0`
 * 3. Send chunks: 4-byte big-endian length prefix + data
 * 4. Send terminator: 4 zero bytes
 * 5. Read response: `stream: OK\0` = clean, `stream: <VirusName> FOUND\0` = infected
 */
export class ScanFile extends BaseHandler<I, O> implements IScanFile {
  private readonly config: ClamdConfig;
  private readonly options: FilesScanningOptions;

  constructor(config: ClamdConfig, options: FilesScanningOptions, context: IHandlerContext) {
    super(context);
    this.config = config;
    this.options = options;
  }

  override get redaction(): RedactionSpec {
    return { suppressInput: true };
  }

  protected async executeAsync(input: I): Promise<D2Result<O | undefined>> {
    try {
      const response = await this.sendToClam(input.buffer);
      // ClamAV responses are null-terminated (e.g., "stream: OK\0").
      // Strip null bytes and whitespace before parsing.
      const responseStr = response.toString("utf8").replace(/\0/g, "").trim();

      if (responseStr.endsWith("OK")) {
        return D2Result.ok({ data: { clean: true } });
      }

      const foundMatch = responseStr.match(/^stream:\s*(.+)\s+FOUND$/i);
      if (foundMatch) {
        const threat = foundMatch[1]!;
        return D2Result.ok({ data: { clean: false, threat } });
      }

      // Unexpected response format
      this.context.logger.warn("Unexpected ClamAV response", { response: responseStr });
      return D2Result.serviceUnavailable();
    } catch (err: unknown) {
      this.context.logger.error("ClamAV scan failed", { err });
      return D2Result.serviceUnavailable();
    }
  }

  private sendToClam(buffer: Buffer): Promise<Buffer> {
    return new Promise((resolve, reject) => {
      const socket = new Socket();
      const chunks: Buffer[] = [];

      socket.setTimeout(this.options.socketTimeoutMs);

      socket.on("data", (chunk) => chunks.push(chunk));
      socket.on("end", () => {
        socket.destroy();
        resolve(Buffer.concat(chunks));
      });
      socket.on("error", (err) => {
        socket.destroy();
        reject(err);
      });
      socket.on("timeout", () => {
        socket.destroy(new Error("ClamAV connection timed out"));
      });

      socket.connect(this.config.port, this.config.host, () => {
        // Send INSTREAM command
        socket.write("zINSTREAM\0");

        // Send data in chunks with 4-byte big-endian length prefix
        const chunkSize = 8192;
        for (let i = 0; i < buffer.length; i += chunkSize) {
          const slice = buffer.subarray(i, i + chunkSize);
          const header = Buffer.alloc(4);
          header.writeUInt32BE(slice.length, 0);
          socket.write(header);
          socket.write(slice);
        }

        // Send terminator (4 zero bytes)
        socket.write(Buffer.alloc(4, 0));
      });
    });
  }
}
