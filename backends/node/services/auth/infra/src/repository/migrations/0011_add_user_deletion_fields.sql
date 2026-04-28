ALTER TABLE "user" ADD COLUMN "status" text DEFAULT 'active' NOT NULL;--> statement-breakpoint
ALTER TABLE "user" ADD COLUMN "deleted_at" timestamp;--> statement-breakpoint
ALTER TABLE "user" ADD COLUMN "deletion_feedback" jsonb;--> statement-breakpoint
CREATE INDEX "user_pending_deletion_idx" ON "user" USING btree ("deleted_at") WHERE "user"."status" = 'pending_deletion';