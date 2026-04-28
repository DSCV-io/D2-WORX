import { BaseHandler, type IHandlerContext, validators } from "@d2/handler";
import { D2Result } from "@d2/result";
import { handleGrpcCall } from "@d2/result-extensions";
import type { GeoServiceClient, CreateContactsResponse } from "@d2/protos";
import { Metadata } from "@grpc/grpc-js";
import { z } from "zod";
import type { GeoClientOptions } from "../../geo-client-options.js";
import { Commands } from "../../interfaces/index.js";

type Input = Commands.CreateContactsInput;
type Output = Commands.CreateContactsOutput;

/**
 * Handler for creating Geo contacts via gRPC.
 * Validates context keys against allowedContextKeys before calling gRPC.
 * Mirrors D2.Geo.Client.CQRS.Handlers.C.CreateContacts in .NET.
 */
export class CreateContacts
  extends BaseHandler<Input, Output>
  implements Commands.ICreateContactsHandler
{
  override get redaction() {
    return Commands.CREATE_CONTACTS_REDACTION;
  }

  private readonly geoClient: GeoServiceClient;
  private readonly options: GeoClientOptions;
  private readonly inputSchema: z.ZodType<Input>;

  constructor(geoClient: GeoServiceClient, options: GeoClientOptions, context: IHandlerContext) {
    super(context);
    this.geoClient = geoClient;
    this.options = options;
    this.inputSchema = z
      .object({
        contacts: z.array(
          z
            .object({ contextKey: validators.zodAllowedContextKey(options.allowedContextKeys) })
            .passthrough(),
        ),
      })
      .passthrough() as unknown as z.ZodType<Input>;
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(this.inputSchema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const r = await handleGrpcCall(
      () =>
        new Promise<CreateContactsResponse>((resolve, reject) => {
          this.geoClient.createContacts(
            { contactsToCreate: input.contacts },
            new Metadata(),
            { deadline: Date.now() + this.options.grpcTimeoutMs },
            (err, res) => {
              if (err) reject(err);
              else resolve(res);
            },
          );
        }),
      (res) => res.result!,
      (res) => ({ data: res.data ?? [] }),
    );

    return D2Result.bubble(r, r.data);
  }
}

export type {
  CreateContactsInput,
  CreateContactsOutput,
} from "../../interfaces/c/create-contacts.js";
