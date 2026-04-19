import { z } from "zod";
import { BaseHandler, type IHandlerContext } from "@d2/handler";
import { D2Result } from "@d2/result";
import { TK } from "@d2/i18n";
import type { IGetContactsByExtKeysHandler } from "@d2/geo-client";
import { Commands } from "../../../../interfaces/cqrs/handlers/index.js";

type Input = Commands.SetUserChannelPreferenceInput;
type Output = Commands.SetUserChannelPreferenceOutput;

const schema = z.object({
  contextKey: z.string().min(1).max(64),
  relatedEntityId: z.string().min(1).max(64),
  emailEnabled: z.boolean().optional(),
  smsEnabled: z.boolean().optional(),
});

/**
 * User-centric Set — resolves the user's contact via Geo (memory-cached) and
 * delegates to the contact-id Set handler. Returns notFound if the user has no
 * contact yet (cannot set prefs with no contact to attach them to).
 */
export class SetUserChannelPreference
  extends BaseHandler<Input, Output>
  implements Commands.ISetUserChannelPreferenceHandler
{
  constructor(
    private readonly getContactsByExtKeys: IGetContactsByExtKeysHandler,
    private readonly inner: Commands.ISetChannelPreferenceHandler,
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
      return D2Result.notFound({ messages: [TK.common.errors.NOT_FOUND] });
    }

    const inner = await this.inner.handleAsync({
      contactId: contact.id,
      emailEnabled: input.emailEnabled,
      smsEnabled: input.smsEnabled,
    });
    if (!inner.success || !inner.data) return D2Result.bubbleFail(inner);

    return D2Result.ok({ data: { pref: inner.data.pref } });
  }
}

export type {
  SetUserChannelPreferenceInput,
  SetUserChannelPreferenceOutput,
} from "../../../../interfaces/cqrs/handlers/c/set-user-channel-preference.js";
