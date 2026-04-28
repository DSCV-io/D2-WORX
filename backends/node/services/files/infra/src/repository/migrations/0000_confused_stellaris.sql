CREATE TABLE "file" (
	"id" varchar(36) PRIMARY KEY NOT NULL,
	"context_key" varchar(100) NOT NULL,
	"related_entity_id" varchar(255) NOT NULL,
	"status" varchar(20) DEFAULT 'pending' NOT NULL,
	"content_type" varchar(255) NOT NULL,
	"display_name" varchar(255) NOT NULL,
	"size_bytes" bigint NOT NULL,
	"variants" jsonb,
	"rejection_reason" varchar(50),
	"created_at" timestamp DEFAULT now() NOT NULL,
	"updated_at" timestamp DEFAULT now() NOT NULL
);
--> statement-breakpoint
CREATE INDEX "idx_file_context_related" ON "file" USING btree ("context_key","related_entity_id");--> statement-breakpoint
CREATE INDEX "idx_file_status_created" ON "file" USING btree ("status","created_at");