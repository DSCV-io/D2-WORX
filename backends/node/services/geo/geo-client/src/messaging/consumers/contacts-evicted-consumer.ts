import type { ILogger } from "@d2/logging";
import type { MessageBus, ConsumerConfig, IMessageConsumer } from "@d2/messaging";
import type { ContactsEvictedEvent } from "@d2/protos";
import { ContactsEvictedEventFns } from "@d2/protos";
import type { ContactsEvicted } from "../handlers/sub/contacts-evicted.js";

/**
 * Consumer bridge for ContactsEvicted events.
 * Creates a RabbitMQ consumer that delegates to the ContactsEvicted handler.
 *
 * Throw on handler failure -> rabbitmq-client nacks with requeue.
 *
 * Mirrors D2.Geo.Client.Messaging.Consumers.ContactEvictionConsumerService in .NET.
 */
export function createContactsEvictedConsumer(
  bus: MessageBus,
  config: ConsumerConfig<ContactsEvictedEvent>,
  handlerFactory: () => ContactsEvicted,
  logger: ILogger,
): IMessageConsumer {
  return bus.subscribe<ContactsEvictedEvent>(
    { ...config, deserialize: ContactsEvictedEventFns.fromJSON },
    async (message) => {
      const contacts = message.contacts ?? [];
      logger.info(`Received ContactsEvicted event for ${contacts.length} contact(s)`);

      const handler = handlerFactory();
      const result = await handler.handleAsync(message);

      if (result.failed) {
        logger.error(`Failed to process ContactsEvicted event`);
        throw new Error(`Failed to process ContactsEvicted event`);
      }

      logger.info(`Successfully processed ContactsEvicted event for ${contacts.length} contact(s)`);
    },
  );
}
