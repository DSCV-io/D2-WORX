import {
  pgTable,
  varchar,
  text,
  boolean,
  integer,
  jsonb,
  timestamp,
  index,
  uniqueIndex,
  type AnyPgColumn,
} from "drizzle-orm/pg-core";

// ---------------------------------------------------------------------------
// message — standalone transactional messages only (threadId always null in Stage A)
// ---------------------------------------------------------------------------
export const message = pgTable(
  "message",
  {
    id: varchar("id", { length: 36 }).primaryKey(),
    threadId: varchar("thread_id", { length: 36 }).references((): AnyPgColumn => message.id, {
      onDelete: "cascade",
    }),
    parentMessageId: varchar("parent_message_id", { length: 36 }).references(
      (): AnyPgColumn => message.id,
      { onDelete: "cascade" },
    ),
    senderUserId: varchar("sender_user_id", { length: 36 }),
    senderContactId: varchar("sender_contact_id", { length: 36 }),
    senderService: varchar("sender_service", { length: 50 }),
    title: varchar("title", { length: 255 }),
    content: text("content").notNull(),
    plainTextContent: text("plain_text_content").notNull(),
    contentFormat: varchar("content_format", { length: 20 }).notNull().default("markdown"),
    channels: text("channels").array().notNull().default([]),
    urgency: varchar("urgency", { length: 20 }).notNull().default("normal"),
    relatedEntityId: varchar("related_entity_id", { length: 36 }),
    relatedEntityType: varchar("related_entity_type", { length: 100 }),
    metadata: jsonb("metadata"),
    editedAt: timestamp("edited_at"),
    deletedAt: timestamp("deleted_at"),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
  },
  (t) => [
    index("idx_message_thread_id").on(t.threadId),
    index("idx_message_sender_user_id").on(t.senderUserId),
  ],
);

// ---------------------------------------------------------------------------
// delivery_request — WHO to deliver to.
// recipientContactId is nullable: undefined for one-shot transient sends to
// addresses provided via alternativeContactInfo (e.g. OTP to unverified email).
// ---------------------------------------------------------------------------
export const deliveryRequest = pgTable(
  "delivery_request",
  {
    id: varchar("id", { length: 36 }).primaryKey(),
    messageId: varchar("message_id", { length: 36 })
      .notNull()
      .references(() => message.id, { onDelete: "cascade" }),
    correlationId: varchar("correlation_id", { length: 36 }).notNull(),
    recipientContactId: varchar("recipient_contact_id", { length: 36 }),
    callbackTopic: varchar("callback_topic", { length: 255 }),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    processedAt: timestamp("processed_at"),
  },
  (t) => [
    index("idx_delivery_request_message_id").on(t.messageId),
    uniqueIndex("idx_delivery_request_correlation_id").on(t.correlationId),
    index("idx_delivery_request_recipient_contact_id").on(t.recipientContactId),
  ],
);

// ---------------------------------------------------------------------------
// delivery_attempt — WHERE we sent (resolved address) + HOW it went
// ---------------------------------------------------------------------------
export const deliveryAttempt = pgTable(
  "delivery_attempt",
  {
    id: varchar("id", { length: 36 }).primaryKey(),
    requestId: varchar("request_id", { length: 36 })
      .notNull()
      .references(() => deliveryRequest.id, { onDelete: "cascade" }),
    channel: varchar("channel", { length: 20 }).notNull(),
    recipientAddress: varchar("recipient_address", { length: 320 }).notNull(),
    status: varchar("status", { length: 20 }).notNull().default("pending"),
    providerMessageId: varchar("provider_message_id", { length: 255 }),
    error: text("error"),
    attemptNumber: integer("attempt_number").notNull(),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    nextRetryAt: timestamp("next_retry_at"),
  },
  (t) => [
    index("idx_delivery_attempt_request_id").on(t.requestId),
    index("idx_delivery_attempt_status_retry").on(t.status, t.nextRetryAt),
    uniqueIndex("uq_delivery_attempt_request_channel_attempt").on(
      t.requestId,
      t.channel,
      t.attemptNumber,
    ),
  ],
);

// ---------------------------------------------------------------------------
// channel_preference — per-contact channel preferences
// ---------------------------------------------------------------------------
export const channelPreference = pgTable(
  "channel_preference",
  {
    id: varchar("id", { length: 36 }).primaryKey(),
    contactId: varchar("contact_id", { length: 36 }).notNull(),
    emailEnabled: boolean("email_enabled").notNull().default(true),
    smsEnabled: boolean("sms_enabled").notNull().default(true),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
  },
  (t) => [uniqueIndex("idx_channel_pref_contact_id").on(t.contactId)],
);
