import { describe, it, expect } from "vitest";
import {
  createThreadParticipant,
  updateThreadParticipant,
  markParticipantLeft,
  CommsValidationError,
} from "@d2/comms-domain";

describe("ThreadParticipant", () => {
  const validInput = {
    threadId: "thread-123",
    userId: "user-456",
    role: "participant" as const,
  };

  describe("createThreadParticipant", () => {
    it("should create a participant with valid input", () => {
      const p = createThreadParticipant(validInput);
      expect(p.threadId).toBe("thread-123");
      expect(p.userId).toBe("user-456");
      expect(p.role).toBe("participant");
      expect(p.notificationsMuted).toBe(false);
      expect(p.lastReadAt).toBeUndefined();
      expect(p.leftAt).toBeUndefined();
      expect(p.joinedAt).toBeInstanceOf(Date);
      expect(p.id).toHaveLength(36);
    });

    it("should accept contactId instead of userId", () => {
      const p = createThreadParticipant({
        threadId: "thread-1",
        contactId: "contact-1",
        role: "observer",
      });
      expect(p.contactId).toBe("contact-1");
      expect(p.userId).toBeUndefined();
    });

    it("should accept notificationsMuted", () => {
      const p = createThreadParticipant({ ...validInput, notificationsMuted: true });
      expect(p.notificationsMuted).toBe(true);
    });

    it("should throw when threadId is empty", () => {
      expect(() => createThreadParticipant({ ...validInput, threadId: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when no identity is provided", () => {
      expect(() => createThreadParticipant({ threadId: "t-1", role: "participant" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when role is invalid", () => {
      expect(() => createThreadParticipant({ ...validInput, role: "admin" as never })).toThrow(
        CommsValidationError,
      );
    });

    it.each(["observer", "participant", "moderator", "creator"] as const)(
      "should accept valid role '%s'",
      (role) => {
        const p = createThreadParticipant({ ...validInput, role });
        expect(p.role).toBe(role);
      },
    );

    it("should accept both userId and contactId simultaneously", () => {
      const p = createThreadParticipant({
        threadId: "thread-1",
        userId: "user-1",
        contactId: "contact-1",
        role: "participant",
      });
      expect(p.userId).toBe("user-1");
      expect(p.contactId).toBe("contact-1");
    });

    it("should set joinedAt and updatedAt to the same value", () => {
      const p = createThreadParticipant(validInput);
      expect(p.joinedAt.getTime()).toBe(p.updatedAt.getTime());
    });
  });

  describe("updateThreadParticipant", () => {
    const baseParticipant = createThreadParticipant(validInput);

    it("should update role", () => {
      const updated = updateThreadParticipant(baseParticipant, { role: "moderator" });
      expect(updated.role).toBe("moderator");
    });

    it("should update notificationsMuted", () => {
      const updated = updateThreadParticipant(baseParticipant, { notificationsMuted: true });
      expect(updated.notificationsMuted).toBe(true);
    });

    it("should update lastReadAt", () => {
      const readAt = new Date();
      const updated = updateThreadParticipant(baseParticipant, { lastReadAt: readAt });
      expect(updated.lastReadAt).toBe(readAt);
    });

    it("should preserve unchanged fields", () => {
      const updated = updateThreadParticipant(baseParticipant, { notificationsMuted: true });
      expect(updated.threadId).toBe(baseParticipant.threadId);
      expect(updated.userId).toBe(baseParticipant.userId);
      expect(updated.role).toBe(baseParticipant.role);
    });

    it("should throw for invalid role on update", () => {
      expect(() => updateThreadParticipant(baseParticipant, { role: "owner" as never })).toThrow(
        CommsValidationError,
      );
    });

    it("should clear lastReadAt by setting to null", () => {
      const withRead = updateThreadParticipant(baseParticipant, { lastReadAt: new Date() });
      expect(withRead.lastReadAt).not.toBeNull();
      const cleared = updateThreadParticipant(withRead, { lastReadAt: null });
      expect(cleared.lastReadAt).toBeNull();
    });

    it("should accept arbitrary dates for lastReadAt", () => {
      const pastDate = new Date("2020-01-01");
      const updated = updateThreadParticipant(baseParticipant, { lastReadAt: pastDate });
      expect(updated.lastReadAt).toBe(pastDate);
    });
  });

  describe("markParticipantLeft", () => {
    it("should set leftAt", () => {
      const p = createThreadParticipant(validInput);
      const left = markParticipantLeft(p);
      expect(left.leftAt).toBeInstanceOf(Date);
      expect(left.leftAt).not.toBeNull();
    });

    it("should update updatedAt", () => {
      const p = createThreadParticipant(validInput);
      const left = markParticipantLeft(p);
      expect(left.updatedAt.getTime()).toBeGreaterThanOrEqual(p.updatedAt.getTime());
    });

    it("should preserve other fields", () => {
      const p = createThreadParticipant(validInput);
      const left = markParticipantLeft(p);
      expect(left.id).toBe(p.id);
      expect(left.threadId).toBe(p.threadId);
      expect(left.role).toBe(p.role);
    });
  });
});
