import { z } from "zod";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import type { IGetContactsByExtKeysHandler } from "@d2/geo-client";
import { Queries } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Queries.GetUserChannelPreferenceInput;
type Output = Queries.GetUserChannelPreferenceOutput;

const schema = z.object({
  contextKey: z.string().min(1).max(64),
  relatedEntityId: z.string().min(1).max(64),
});

/**
 * User-centric Get — resolves the user's contact via Geo (memory-cached) and
 * delegates to the contact-id Get handler. Returns `pref: undefined` when the
 * user has no contact yet (vs. has-contact-but-no-prefs which is also pref: undefined).
 * The gateway treats both as "use defaults".
 */
export class GetUserChannelPreference
  extends BaseHandler<Input, Output>
  implements Queries.IGetUserChannelPreferenceHandler
{
  constructor(
    private readonly getContactsByExtKeys: IGetContactsByExtKeysHandler,
    private readonly inner: Queries.IGetChannelPreferenceHandler,
    context: IHandlerContext,
  ) {
    super(context);
  }

  protected async executeAsync(input: Input): Promise<D2Result<Output | undefined>> {
    const validation = this.validateInput(schema, input);
    if (!validation.success) return D2Result.bubbleFail(validation);

    const lookup = await this.getContactsByExtKeys.handleAsync({
      keys: [{ contextKey: input.contextKey, relatedEntityId: input.relatedEntityId }],
    });
    if (!lookup.success) {
      return D2Result.serviceUnavailable({ messages: [TK.common.errors.SERVICE_UNAVAILABLE] });
    }

    const mapKey = `${input.contextKey}:${input.relatedEntityId}`;
    const contact = lookup.data?.data.get(mapKey)?.[0];
    if (!contact?.id) {
      // No contact yet — caller treats this as "no overrides, use defaults".
      return D2Result.ok({ data: { pref: undefined } });
    }

    const inner = await this.inner.handleAsync({ contactId: contact.id });
    if (!inner.success) {
      // notFound from inner = no prefs row — same "use defaults" semantics.
      if (inner.statusCode === 404) return D2Result.ok({ data: { pref: undefined } });
      return D2Result.bubbleFail(inner);
    }

    return D2Result.ok({ data: { pref: inner.data?.pref } });
  }
}

export type {
  GetUserChannelPreferenceInput,
  GetUserChannelPreferenceOutput,
} from "../../../../interfaces/cqrs/handlers/q/get-user-channel-preference.js";
