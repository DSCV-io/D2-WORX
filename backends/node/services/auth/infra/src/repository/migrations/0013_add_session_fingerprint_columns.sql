ALTER TABLE "session" ADD COLUMN "device_fingerprint" text;--> statement-breakpoint
ALTER TABLE "session" ADD COLUMN "client_fingerprint" text;--> statement-breakpoint
ALTER TABLE "session" ADD COLUMN "server_fingerprint" text;--> statement-breakpoint
ALTER TABLE "sign_in_event" ADD COLUMN "client_fingerprint" varchar(64);--> statement-breakpoint
ALTER TABLE "sign_in_event" ADD COLUMN "server_fingerprint" varchar(64);--> statement-breakpoint
CREATE INDEX "session_client_fingerprint_idx" ON "session" USING btree ("client_fingerprint");--> statement-breakpoint
CREATE INDEX "session_server_fingerprint_idx" ON "session" USING btree ("server_fingerprint");--> statement-breakpoint
CREATE INDEX "session_device_fingerprint_idx" ON "session" USING btree ("device_fingerprint");--> statement-breakpoint
CREATE INDEX "idx_sign_in_event_client_fingerprint" ON "sign_in_event" USING btree ("client_fingerprint");--> statement-breakpoint
CREATE INDEX "idx_sign_in_event_server_fingerprint" ON "sign_in_event" USING btree ("server_fingerprint");--> statement-breakpoint
CREATE INDEX "idx_sign_in_event_device_fingerprint" ON "sign_in_event" USING btree ("device_fingerprint");