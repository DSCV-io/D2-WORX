import { BaseHandler, type IHandlerContext, type IHandler } from "@d2/handler";
import { D2Result } from "@d2/result";
import type { ContactsEvictedEvent } from "@d2/protos";
import type { MemoryCacheStore } from "@d2/cache-memory";
import { GEO_CACHE_KEYS } from "../../../cache-keys.js";

export interface ContactsEvictedOutput {}

/** Handler interface for processing contact eviction events. */
export type IContactsEvictedHandler = IHandler<ContactsEvictedEvent, ContactsEvictedOutput>;

/**
 * Messaging subscription handler for contact eviction notifications.
 * Evicts matching entries from the local in-memory cache.
 * Mirrors D2.Geo.Client.Messaging.Handlers.Sub.ContactsEvicted in .NET.
 */
export class ContactsEvicted
  extends BaseHandler<ContactsEvictedEvent, ContactsEvictedOutput>
  implements IContactsEvictedHandler
{
  private readonly store: MemoryCacheStore;

  constructor(store: MemoryCacheStore, context: IHandlerContext) {
    super(context);
    this.store = store;
  }

  protected async executeAsync(
    input: ContactsEvictedEvent,
  ): Promise<D2Result<ContactsEvictedOutput | undefined>> {
    const contacts = input.contacts ?? [];
    for (const contact of contacts) {
      if (!contact.contactId || !contact.contextKey || !contact.relatedEntityId) continue;
      // Evict by contact ID.
      this.store.delete(GEO_CACHE_KEYS.contact(contact.contactId));
      // Evict by ext-key.
      this.store.delete(
        GEO_CACHE_KEYS.contactsByExtKey(contact.contextKey, contact.relatedEntityId),
      );
    }

    this.context.logger.info(`Evicted ${contacts.length} contact(s) from cache`);

    return D2Result.ok({ data: {} });
  }
}
