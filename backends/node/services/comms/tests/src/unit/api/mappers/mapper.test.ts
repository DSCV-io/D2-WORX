import { describe, it, expect } from "vitest";
import type { ChannelPreference, DeliveryRequest, DeliveryAttempt } from "@d2/comms-domain";
import {
  channelPreferenceToProto,
  deliveryRequestToProto,
  deliveryAttemptToProto,
} from "@d2/comms-api";

describe("channelPreferenceToProto", () => {
  it("should map all fields for a full preference", () => {
    const pref: ChannelPreference = {
      id: "pref-1",
      contactId: "contact-1",
      emailEnabled: true,
      smsEnabled: false,
      createdAt: new Date("2026-01-01T00:00:00Z"),
      updatedAt: new Date("2026-01-15T00:00:00Z"),
    };

    const result = channelPreferenceToProto(pref);

    expect(result.id).toBe("pref-1");
    expect(result.contactId).toBe("contact-1");
    expect(result.emailEnabled).toBe(true);
    expect(result.smsEnabled).toBe(false);
    expect(result.createdAt).toEqual(new Date("2026-01-01T00:00:00Z"));
    expect(result.updatedAt).toEqual(new Date("2026-01-15T00:00:00Z"));
  });

  it("should not include removed fields (userId, quietHours)", () => {
    const pref: ChannelPreference = {
      id: "pref-2",
      contactId: "contact-2",
      emailEnabled: true,
      smsEnabled: true,
      createdAt: new Date("2026-01-01T00:00:00Z"),
      updatedAt: new Date("2026-01-01T00:00:00Z"),
    };

    const result = channelPreferenceToProto(pref);

    expect(result).not.toHaveProperty("userId");
    expect(result).not.toHaveProperty("quietHoursStart");
    expect(result).not.toHaveProperty("quietHoursEnd");
    expect(result).not.toHaveProperty("quietHoursTz");
  });
});

describe("deliveryRequestToProto", () => {
  it("should map all fields for a full request", () => {
    const req: DeliveryRequest = {
      id: "req-1",
      messageId: "msg-1",
      correlationId: "corr-1",
      recipientContactId: "contact-1",
      callbackTopic: "callbacks.delivery",
      createdAt: new Date("2026-01-01T00:00:00Z"),
      processedAt: new Date("2026-01-01T00:01:00Z"),
    };

    const result = deliveryRequestToProto(req);

    expect(result.id).toBe("req-1");
    expect(result.messageId).toBe("msg-1");
    expect(result.correlationId).toBe("corr-1");
    expect(result.recipientContactId).toBe("contact-1");
    expect(result.callbackTopic).toBe("callbacks.delivery");
    expect(result.createdAt).toEqual(new Date("2026-01-01T00:00:00Z"));
    expect(result.processedAt).toEqual(new Date("2026-01-01T00:01:00Z"));
  });

  it("should map optional fields to undefined and not include removed fields", () => {
    const req: DeliveryRequest = {
      id: "req-2",
      messageId: "msg-2",
      correlationId: "corr-2",
      recipientContactId: "contact-2",
      createdAt: new Date("2026-01-01T00:00:00Z"),
    };

    const result = deliveryRequestToProto(req);

    expect(result.callbackTopic).toBeUndefined();
    expect(result.processedAt).toBeUndefined();
    // Removed fields should not be present
    expect(result).not.toHaveProperty("recipientUserId");
    expect(result).not.toHaveProperty("channels");
    expect(result).not.toHaveProperty("templateName");
  });
});

describe("deliveryAttemptToProto", () => {
  it("should map all fields for a full attempt", () => {
    const attempt: DeliveryAttempt = {
      id: "att-1",
      requestId: "req-1",
      channel: "email",
      recipientAddress: "user@example.com",
      status: "sent",
      providerMessageId: "resend-123",
      attemptNumber: 1,
      createdAt: new Date("2026-01-01T00:00:00Z"),
    };

    const result = deliveryAttemptToProto(attempt);

    expect(result.id).toBe("att-1");
    expect(result.requestId).toBe("req-1");
    expect(result.channel).toBe("email");
    expect(result.recipientAddress).toBe("user@example.com");
    expect(result.status).toBe("sent");
    expect(result.providerMessageId).toBe("resend-123");
    expect(result.attemptNumber).toBe(1);
  });

  it("should map optional fields to undefined", () => {
    const attempt: DeliveryAttempt = {
      id: "att-2",
      requestId: "req-2",
      channel: "sms",
      recipientAddress: "+15551234567",
      status: "failed",
      error: "Twilio timeout",
      attemptNumber: 3,
      createdAt: new Date("2026-01-01T00:00:00Z"),
      nextRetryAt: new Date("2026-01-01T00:05:00Z"),
    };

    const result = deliveryAttemptToProto(attempt);

    expect(result.providerMessageId).toBeUndefined();
    expect(result.error).toBe("Twilio timeout");
    expect(result.nextRetryAt).toEqual(new Date("2026-01-01T00:05:00Z"));
  });
});
