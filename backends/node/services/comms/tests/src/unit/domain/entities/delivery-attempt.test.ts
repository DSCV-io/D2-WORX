import { describe, it, expect } from "vitest";
import {
  createDeliveryAttempt,
  transitionDeliveryAttemptStatus,
  CommsDomainError,
  CommsValidationError,
} from "@d2/comms-domain";

describe("DeliveryAttempt", () => {
  const validInput = {
    requestId: "req-123",
    channel: "email" as const,
    recipientAddress: "user@example.com",
    attemptNumber: 1,
  };

  describe("createDeliveryAttempt", () => {
    it("should create an attempt in pending status", () => {
      const attempt = createDeliveryAttempt(validInput);
      expect(attempt.status).toBe("pending");
      expect(attempt.requestId).toBe("req-123");
      expect(attempt.channel).toBe("email");
      expect(attempt.recipientAddress).toBe("user@example.com");
      expect(attempt.attemptNumber).toBe(1);
      expect(attempt.id).toHaveLength(36);
      expect(attempt.createdAt).toBeInstanceOf(Date);
    });

    it("should default optional fields to undefined", () => {
      const attempt = createDeliveryAttempt(validInput);
      expect(attempt.providerMessageId).toBeUndefined();
      expect(attempt.error).toBeUndefined();
      expect(attempt.nextRetryAt).toBeUndefined();
    });

    it("should accept sms channel", () => {
      const attempt = createDeliveryAttempt({ ...validInput, channel: "sms" });
      expect(attempt.channel).toBe("sms");
    });

    it("should throw when requestId is empty", () => {
      expect(() => createDeliveryAttempt({ ...validInput, requestId: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when channel is invalid", () => {
      expect(() => createDeliveryAttempt({ ...validInput, channel: "push" as never })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when recipientAddress is empty", () => {
      expect(() => createDeliveryAttempt({ ...validInput, recipientAddress: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when attemptNumber is zero", () => {
      expect(() => createDeliveryAttempt({ ...validInput, attemptNumber: 0 })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when attemptNumber is negative", () => {
      expect(() => createDeliveryAttempt({ ...validInput, attemptNumber: -1 })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when attemptNumber is not integer", () => {
      expect(() => createDeliveryAttempt({ ...validInput, attemptNumber: 1.5 })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when attemptNumber is NaN", () => {
      expect(() => createDeliveryAttempt({ ...validInput, attemptNumber: NaN })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when attemptNumber is Infinity", () => {
      expect(() => createDeliveryAttempt({ ...validInput, attemptNumber: Infinity })).toThrow(
        CommsValidationError,
      );
    });

    it("should accept a pre-generated ID", () => {
      const attempt = createDeliveryAttempt({ ...validInput, id: "custom-id" });
      expect(attempt.id).toBe("custom-id");
    });

    it("should generate unique IDs", () => {
      const a1 = createDeliveryAttempt(validInput);
      const a2 = createDeliveryAttempt(validInput);
      expect(a1.id).not.toBe(a2.id);
    });
  });

  describe("transitionDeliveryAttemptStatus", () => {
    it("should transition from pending to sent", () => {
      const attempt = createDeliveryAttempt(validInput);
      const sent = transitionDeliveryAttemptStatus(attempt, "sent", {
        providerMessageId: "sg-abc",
      });
      expect(sent.status).toBe("sent");
      expect(sent.providerMessageId).toBe("sg-abc");
    });

    it("should transition from pending to failed with error", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed", {
        error: "SMTP timeout",
        nextRetryAt: new Date("2026-03-01T12:00:00Z"),
      });
      expect(failed.status).toBe("failed");
      expect(failed.error).toBe("SMTP timeout");
      expect(failed.nextRetryAt).toEqual(new Date("2026-03-01T12:00:00Z"));
    });

    it("should transition from failed to retried", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed", {
        error: "timeout",
      });
      const retried = transitionDeliveryAttemptStatus(failed, "retried");
      expect(retried.status).toBe("retried");
    });

    it("should throw for invalid transition from sent", () => {
      const attempt = createDeliveryAttempt(validInput);
      const sent = transitionDeliveryAttemptStatus(attempt, "sent");
      expect(() => transitionDeliveryAttemptStatus(sent, "failed")).toThrow(CommsDomainError);
    });

    it("should throw for invalid transition from retried", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed");
      const retried = transitionDeliveryAttemptStatus(failed, "retried");
      expect(() => transitionDeliveryAttemptStatus(retried, "pending")).toThrow(CommsDomainError);
    });

    it("should throw for invalid transition from pending to retried", () => {
      const attempt = createDeliveryAttempt(validInput);
      expect(() => transitionDeliveryAttemptStatus(attempt, "retried")).toThrow(CommsDomainError);
    });

    it("should preserve fields not in options", () => {
      const attempt = createDeliveryAttempt(validInput);
      const sent = transitionDeliveryAttemptStatus(attempt, "sent");
      expect(sent.id).toBe(attempt.id);
      expect(sent.requestId).toBe(attempt.requestId);
      expect(sent.channel).toBe(attempt.channel);
      expect(sent.attemptNumber).toBe(attempt.attemptNumber);
    });

    it("should allow clearing nextRetryAt via null", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed", {
        nextRetryAt: new Date(),
      });
      expect(failed.nextRetryAt).not.toBeNull();

      const retried = transitionDeliveryAttemptStatus(failed, "retried", {
        nextRetryAt: null,
      });
      expect(retried.nextRetryAt).toBeNull();
    });

    it("should preserve providerMessageId when not in options", () => {
      const attempt = createDeliveryAttempt(validInput);
      const sent = transitionDeliveryAttemptStatus(attempt, "sent", {
        providerMessageId: "sg-abc",
      });
      // providerMessageId already set, transition with only error — should keep providerMessageId
      expect(sent.providerMessageId).toBe("sg-abc");
      expect(sent.error).toBeUndefined();
    });

    it("should throw for failed → sent (invalid transition)", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed");
      expect(() => transitionDeliveryAttemptStatus(failed, "sent")).toThrow(CommsDomainError);
    });

    it("should throw for failed → pending (invalid transition)", () => {
      const attempt = createDeliveryAttempt(validInput);
      const failed = transitionDeliveryAttemptStatus(attempt, "failed");
      expect(() => transitionDeliveryAttemptStatus(failed, "pending")).toThrow(CommsDomainError);
    });

    it("should support full lifecycle: pending → failed → retried", () => {
      const attempt = createDeliveryAttempt(validInput);
      expect(attempt.status).toBe("pending");

      const failed = transitionDeliveryAttemptStatus(attempt, "failed", {
        error: "SMTP timeout",
        nextRetryAt: new Date("2026-03-01T12:00:05Z"),
      });
      expect(failed.status).toBe("failed");
      expect(failed.error).toBe("SMTP timeout");
      expect(failed.nextRetryAt).not.toBeNull();

      const retried = transitionDeliveryAttemptStatus(failed, "retried");
      expect(retried.status).toBe("retried");
      expect(retried.error).toBe("SMTP timeout"); // error preserved from failed
    });

    it("should support full lifecycle: pending → sent", () => {
      const attempt = createDeliveryAttempt(validInput);
      const sent = transitionDeliveryAttemptStatus(attempt, "sent", {
        providerMessageId: "twilio-sid-123",
      });
      expect(sent.status).toBe("sent");
      expect(sent.providerMessageId).toBe("twilio-sid-123");
      expect(sent.error).toBeUndefined();
    });
  });
});
