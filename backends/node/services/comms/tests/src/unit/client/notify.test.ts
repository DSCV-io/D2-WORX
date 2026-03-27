import { describe, it, expect, beforeEach, vi } from "vitest";
import { Notify } from "@d2/comms-client";
import type { NotifyInput } from "@d2/comms-client";
import type { IHandlerContext } from "@d2/handler";
import type { IMessagePublisher } from "@d2/messaging";

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function createMockContext(): IHandlerContext {
  return {
    request: { traceId: "test-trace-id" },
    logger: {
      info: vi.fn(),
      warn: vi.fn(),
      error: vi.fn(),
      debug: vi.fn(),
    },
  } as unknown as IHandlerContext;
}

function createMockPublisher(): IMessagePublisher {
  return {
    send: vi.fn().mockResolvedValue(undefined),
    close: vi.fn().mockResolvedValue(undefined),
  };
}

function validInput(overrides: Partial<NotifyInput> = {}): NotifyInput {
  return {
    recipientContactId: "019505e1-4a28-7000-8000-000000000001",
    title: "Test Notification",
    content: "# Hello\n\nThis is a test notification.",
    plaintext: "Hello. This is a test notification.",
    correlationId: "corr-123",
    senderService: "auth",
    ...overrides,
  };
}

// ---------------------------------------------------------------------------
// Tests
// ---------------------------------------------------------------------------

describe("Notify", () => {
  let context: IHandlerContext;
  let publisher: IMessagePublisher;
  let handler: Notify;

  beforeEach(() => {
    context = createMockContext();
    publisher = createMockPublisher();
    handler = new Notify(context, publisher);
  });

  // -------------------------------------------------------------------------
  // Validation failures
  // -------------------------------------------------------------------------

  describe("validation", () => {
    it("should reject invalid UUID for recipientContactId", async () => {
      const result = await handler.handleAsync(validInput({ recipientContactId: "not-a-uuid" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.length).toBeGreaterThan(0);
      expect(result.inputErrors.some((e) => e[0] === "recipientContactId")).toBe(true);
    });

    it("should reject empty title", async () => {
      const result = await handler.handleAsync(validInput({ title: "" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "title")).toBe(true);
    });

    it("should reject title exceeding 255 characters", async () => {
      const result = await handler.handleAsync(validInput({ title: "a".repeat(256) }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "title")).toBe(true);
    });

    it("should accept title at exactly 255 characters", async () => {
      const result = await handler.handleAsync(validInput({ title: "a".repeat(255) }));

      expect(result).toBeSuccess();
    });

    it("should reject empty content", async () => {
      const result = await handler.handleAsync(validInput({ content: "" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "content")).toBe(true);
    });

    it("should reject content exceeding 50,000 characters", async () => {
      const result = await handler.handleAsync(validInput({ content: "x".repeat(50_001) }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "content")).toBe(true);
    });

    it("should reject empty plaintext", async () => {
      const result = await handler.handleAsync(validInput({ plaintext: "" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "plaintext")).toBe(true);
    });

    it("should reject invalid urgency value", async () => {
      const result = await handler.handleAsync(
        validInput({ urgency: "critical" as "normal" | "urgent" }),
      );

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "urgency")).toBe(true);
    });

    it("should reject empty correlationId", async () => {
      const result = await handler.handleAsync(validInput({ correlationId: "" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "correlationId")).toBe(true);
    });

    it("should reject empty senderService", async () => {
      const result = await handler.handleAsync(validInput({ senderService: "" }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "senderService")).toBe(true);
    });
  });

  // -------------------------------------------------------------------------
  // No-publisher fallback
  // -------------------------------------------------------------------------

  describe("no-publisher fallback", () => {
    it("should log and return success when no publisher is provided", async () => {
      const noPublisherHandler = new Notify(context);
      const input = validInput();

      const result = await noPublisherHandler.handleAsync(input);

      expect(result).toBeSuccess();
      expect(result.data).toEqual({});
      expect(context.logger.info).toHaveBeenCalledWith(
        "No publisher configured — notification logged but not sent",
        expect.objectContaining({
          recipientContactId: input.recipientContactId,
          title: input.title,
          senderService: input.senderService,
          correlationId: input.correlationId,
        }),
      );
    });

    it("should not attempt to publish when no publisher is provided", async () => {
      const noPublisherHandler = new Notify(context);

      await noPublisherHandler.handleAsync(validInput());

      // publisher.send should never be called (publisher doesn't even exist)
      expect(publisher.send).not.toHaveBeenCalled();
    });
  });

  // -------------------------------------------------------------------------
  // Successful publish
  // -------------------------------------------------------------------------

  describe("successful publish", () => {
    it("should pass all required fields to publisher.send", async () => {
      const input = validInput();

      const result = await handler.handleAsync(input);

      expect(result).toBeSuccess();
      expect(publisher.send).toHaveBeenCalledOnce();
      expect(publisher.send).toHaveBeenCalledWith(
        expect.objectContaining({
          exchange: "comms.notifications",
          routingKey: "",
        }),
        expect.objectContaining({
          recipientContactId: input.recipientContactId,
          title: input.title,
          content: input.content,
          plaintext: input.plaintext,
          correlationId: input.correlationId,
          senderService: input.senderService,
        }),
      );
    });

    it("should use correct exchange name", async () => {
      await handler.handleAsync(validInput());

      const [target] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(target.exchange).toBe("comms.notifications");
    });

    it("should use empty string as routing key", async () => {
      await handler.handleAsync(validInput());

      const [target] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(target.routingKey).toBe("");
    });

    it("should default channels to empty array when not provided", async () => {
      await handler.handleAsync(validInput());

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.channels).toEqual([]);
    });

    it("should pass channels through when explicitly set", async () => {
      await handler.handleAsync(validInput({ channels: ["email"] }));

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.channels).toEqual(["email"]);
    });

    it("should default urgency to 'normal' when not provided", async () => {
      await handler.handleAsync(validInput());

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.urgency).toBe("normal");
    });

    it("should pass urgency through when explicitly set to 'urgent'", async () => {
      await handler.handleAsync(validInput({ urgency: "urgent" }));

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.urgency).toBe("urgent");
    });

    it("should pass metadata through when provided", async () => {
      const metadata = { templateId: "welcome-v2", locale: "en-US" };

      await handler.handleAsync(validInput({ metadata }));

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.metadata).toEqual(metadata);
    });

    it("should pass metadata as undefined when not provided", async () => {
      await handler.handleAsync(validInput());

      const [, message] = (publisher.send as ReturnType<typeof vi.fn>).mock.calls[0];
      expect(message.metadata).toBeUndefined();
    });

    it("should return empty object as data on success", async () => {
      const result = await handler.handleAsync(validInput());

      expect(result).toBeSuccess();
      expect(result.data).toEqual({});
    });
  });

  // -------------------------------------------------------------------------
  // Publisher error handling
  // -------------------------------------------------------------------------

  describe("publisher error handling", () => {
    it("should return serviceUnavailable when publisher.send throws", async () => {
      (publisher.send as ReturnType<typeof vi.fn>).mockRejectedValue(
        new Error("RabbitMQ connection lost"),
      );

      const result = await handler.handleAsync(validInput());

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(503);
      expect(result).toHaveErrorCode("SERVICE_UNAVAILABLE");
    });

    it("should return serviceUnavailable for non-Error throws", async () => {
      (publisher.send as ReturnType<typeof vi.fn>).mockRejectedValue("connection timeout");

      const result = await handler.handleAsync(validInput());

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(503);
      expect(result).toHaveErrorCode("SERVICE_UNAVAILABLE");
    });
  });

  // -------------------------------------------------------------------------
  // Boundary validation
  // -------------------------------------------------------------------------

  describe("boundary validation", () => {
    it("should accept correlationId at exactly 36 characters", async () => {
      const result = await handler.handleAsync(validInput({ correlationId: "a".repeat(36) }));

      expect(result).toBeSuccess();
    });

    it("should reject correlationId at 37 characters", async () => {
      const result = await handler.handleAsync(validInput({ correlationId: "a".repeat(37) }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "correlationId")).toBe(true);
    });

    it("should accept senderService at exactly 50 characters", async () => {
      const result = await handler.handleAsync(validInput({ senderService: "s".repeat(50) }));

      expect(result).toBeSuccess();
    });

    it("should reject senderService at 51 characters", async () => {
      const result = await handler.handleAsync(validInput({ senderService: "s".repeat(51) }));

      expect(result).toBeFailure();
      expect(result).toHaveStatusCode(400);
      expect(result).toHaveErrorCode("VALIDATION_FAILED");
      expect(result.inputErrors.some((e) => e[0] === "senderService")).toBe(true);
    });

    it("should accept content at exactly 50,000 characters", async () => {
      const result = await handler.handleAsync(validInput({ content: "c".repeat(50_000) }));

      expect(result).toBeSuccess();
    });

    it("should accept plaintext at exactly 50,000 characters", async () => {
      const result = await handler.handleAsync(validInput({ plaintext: "p".repeat(50_000) }));

      expect(result).toBeSuccess();
    });

    it("should accept title at exactly 255 characters", async () => {
      const result = await handler.handleAsync(validInput({ title: "t".repeat(255) }));

      expect(result).toBeSuccess();
    });
  });
});
