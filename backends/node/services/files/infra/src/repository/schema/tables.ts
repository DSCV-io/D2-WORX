import { pgTable, varchar, bigint, jsonb, timestamp, index } from "drizzle-orm/pg-core";

export const file = pgTable(
  "file",
  {
    id: varchar("id", { length: 36 }).primaryKey(),
    contextKey: varchar("context_key", { length: 100 }).notNull(),
    relatedEntityId: varchar("related_entity_id", { length: 255 }).notNull(),
    uploaderUserId: varchar("uploader_user_id", { length: 36 }).notNull(),
    status: varchar("status", { length: 20 }).notNull().default("pending"),
    contentType: varchar("content_type", { length: 255 }).notNull(),
    displayName: varchar("display_name", { length: 255 }).notNull(),
    sizeBytes: bigint("size_bytes", { mode: "number" }).notNull(),
    variants: jsonb("variants"),
    rejectionReason: varchar("rejection_reason", { length: 50 }),
    createdAt: timestamp("created_at").notNull().defaultNow(),
    updatedAt: timestamp("updated_at").notNull().defaultNow(),
  },
  (t) => [
    index("idx_file_context_related").on(t.contextKey, t.relatedEntityId),
    index("idx_file_status_created").on(t.status, t.createdAt),
    // Cleanup job (`GetStaleFiles`) filters by `(status, updatedAt <= cutoff)`.
    // Without this index, the planner falls back to filtering by the
    // status-only prefix of `idx_file_status_created` and sorting in-memory.
    index("idx_file_status_updated").on(t.status, t.updatedAt),
  ],
);
