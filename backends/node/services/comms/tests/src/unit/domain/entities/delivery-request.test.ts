import { describe, it, expect } from "vitest";
import {
  createDeliveryRequest,
  markDeliveryRequestProcessed,
  CommsValidationError,
} from "@d2/comms-domain";

describe("DeliveryRequest", () => {
  const validInput = {
    messageId: "msg-123",
    correlationId: "corr-456",
    recipientContactId: "contact-789",
  };

  describe("createDeliveryRequest", () => {
    it("should create a delivery request with valid input", () => {
      const req = createDeliveryRequest(validInput);
      expect(req.messageId).toBe("msg-123");
      expect(req.correlationId).toBe("corr-456");
      expect(req.recipientContactId).toBe("contact-789");
      expect(req.id).toHaveLength(36);
      expect(req.createdAt).toBeInstanceOf(Date);
      expect(req.processedAt).toBeUndefined();
    });

    it("should generate unique IDs", () => {
      const req1 = createDeliveryRequest(validInput);
      const req2 = createDeliveryRequest(validInput);
      expect(req1.id).not.toBe(req2.id);
    });

    it("should accept a pre-generated ID", () => {
      const req = createDeliveryRequest({ ...validInput, id: "custom-id" });
      expect(req.id).toBe("custom-id");
    });

    it("should default optional fields to undefined", () => {
      const req = createDeliveryRequest(validInput);
      expect(req.callbackTopic).toBeUndefined();
    });

    it("should accept callbackTopic", () => {
      const req = createDeliveryRequest({ ...validInput, callbackTopic: "signup.complete" });
      expect(req.callbackTopic).toBe("signup.complete");
    });

    // --- Validation errors ---

    it("should throw when messageId is empty", () => {
      expect(() => createDeliveryRequest({ ...validInput, messageId: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when correlationId is empty", () => {
      expect(() => createDeliveryRequest({ ...validInput, correlationId: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should accept undefined recipientContactId (transient sends use alternativeContactInfo at app layer)", () => {
      const req = createDeliveryRequest({
        messageId: "msg-1",
        correlationId: "corr-1",
      });
      expect(req.recipientContactId).toBeUndefined();
    });

    it("should accept empty-string recipientContactId by treating it as undefined-equivalent (XOR enforced at API layer)", () => {
      // Domain entity is permissive; the XOR rule between recipientContactId and
      // alternativeContactInfo is enforced by the Deliver handler's Zod schema.
      const req = createDeliveryRequest({
        messageId: "msg-1",
        correlationId: "corr-1",
        recipientContactId: undefined,
      });
      expect(req.recipientContactId).toBeUndefined();
    });
  });

  describe("markDeliveryRequestProcessed", () => {
    it("should set processedAt", () => {
      const req = createDeliveryRequest(validInput);
      const processed = markDeliveryRequestProcessed(req);
      expect(processed.processedAt).toBeInstanceOf(Date);
      expect(processed.processedAt).not.toBeNull();
    });

    it("should preserve other fields", () => {
      const req = createDeliveryRequest(validInput);
      const processed = markDeliveryRequestProcessed(req);
      expect(processed.id).toBe(req.id);
      expect(processed.messageId).toBe(req.messageId);
      expect(processed.correlationId).toBe(req.correlationId);
      expect(processed.createdAt).toBe(req.createdAt);
    });
  });
});
