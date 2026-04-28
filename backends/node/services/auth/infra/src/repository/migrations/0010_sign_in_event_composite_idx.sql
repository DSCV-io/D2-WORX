DROP INDEX "idx_sign_in_event_user_id";--> statement-breakpoint
CREATE INDEX "idx_sign_in_event_user_id_created_at" ON "sign_in_event" USING btree ("user_id","created_at" DESC);