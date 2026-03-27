-- Replace sensitive boolean with channels text array
ALTER TABLE "message" DROP COLUMN "sensitive";
ALTER TABLE "message" ADD COLUMN "channels" text[] DEFAULT '{}' NOT NULL;

-- Add FK: delivery_request.message_id → message.id
ALTER TABLE "delivery_request"
  ADD CONSTRAINT "delivery_request_message_id_message_id_fk"
  FOREIGN KEY ("message_id") REFERENCES "message"("id") ON DELETE cascade ON UPDATE no action;

-- Add FK: delivery_attempt.request_id → delivery_request.id
ALTER TABLE "delivery_attempt"
  ADD CONSTRAINT "delivery_attempt_request_id_delivery_request_id_fk"
  FOREIGN KEY ("request_id") REFERENCES "delivery_request"("id") ON DELETE cascade ON UPDATE no action;

-- Add FK: message.thread_id → message.id (self-ref)
ALTER TABLE "message"
  ADD CONSTRAINT "message_thread_id_message_id_fk"
  FOREIGN KEY ("thread_id") REFERENCES "message"("id") ON DELETE cascade ON UPDATE no action;

-- Add FK: message.parent_message_id → message.id (self-ref)
ALTER TABLE "message"
  ADD CONSTRAINT "message_parent_message_id_message_id_fk"
  FOREIGN KEY ("parent_message_id") REFERENCES "message"("id") ON DELETE cascade ON UPDATE no action;
