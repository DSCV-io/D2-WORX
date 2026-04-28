import { describe, it, expect } from "vitest";
import {
  createThread,
  updateThread,
  transitionThreadState,
  CommsDomainError,
  CommsValidationError,
  THREAD_CONSTRAINTS,
} from "@d2/comms-domain";

describe("Thread", () => {
  const validInput = { type: "chat" as const };

  describe("createThread", () => {
    it("should create a thread in active state", () => {
      const thread = createThread(validInput);
      expect(thread.type).toBe("chat");
      expect(thread.state).toBe("active");
      expect(thread.notificationPolicy).toBe("all_messages");
      expect(thread.id).toHaveLength(36);
      expect(thread.createdAt).toBeInstanceOf(Date);
    });

    it("should default optional fields to undefined", () => {
      const thread = createThread(validInput);
      expect(thread.title).toBeUndefined();
      expect(thread.slug).toBeUndefined();
      expect(thread.orgId).toBeUndefined();
      expect(thread.createdByUserId).toBeUndefined();
    });

    it("should accept all optional fields", () => {
      const thread = createThread({
        type: "forum",
        title: "Q3 Contract Discussion",
        slug: "q3-contract-discussion",
        notificationPolicy: "mentions_only",
        orgId: "org-1",
        createdByUserId: "user-1",
      });
      expect(thread.type).toBe("forum");
      expect(thread.title).toBe("Q3 Contract Discussion");
      expect(thread.slug).toBe("q3-contract-discussion");
      expect(thread.notificationPolicy).toBe("mentions_only");
      expect(thread.orgId).toBe("org-1");
      expect(thread.createdByUserId).toBe("user-1");
    });

    it("should accept valid slug with numbers and hyphens", () => {
      const thread = createThread({ ...validInput, slug: "my-thread-123" });
      expect(thread.slug).toBe("my-thread-123");
    });

    it("should throw for invalid thread type", () => {
      expect(() => createThread({ type: "dm" as never })).toThrow(CommsValidationError);
    });

    it("should throw when title exceeds max length", () => {
      const longTitle = "x".repeat(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH + 1);
      expect(() => createThread({ ...validInput, title: longTitle })).toThrow(CommsValidationError);
    });

    it("should throw when slug contains uppercase", () => {
      expect(() => createThread({ ...validInput, slug: "My-Thread" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when slug contains spaces", () => {
      expect(() => createThread({ ...validInput, slug: "my thread" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when slug contains special characters", () => {
      expect(() => createThread({ ...validInput, slug: "my_thread!" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw for invalid notification policy", () => {
      expect(() =>
        createThread({ ...validInput, notificationPolicy: "everything" as never }),
      ).toThrow(CommsValidationError);
    });

    // --- Slug edge cases ---

    it("should accept slug with only numbers", () => {
      const thread = createThread({ ...validInput, slug: "12345" });
      expect(thread.slug).toBe("12345");
    });

    it("should accept slug with only hyphens", () => {
      const thread = createThread({ ...validInput, slug: "---" });
      expect(thread.slug).toBe("---");
    });

    it("should throw when slug contains underscores", () => {
      expect(() => createThread({ ...validInput, slug: "my_thread" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when slug contains dots", () => {
      expect(() => createThread({ ...validInput, slug: "my.thread" })).toThrow(
        CommsValidationError,
      );
    });

    it("should return undefined slug when slug is whitespace only", () => {
      const thread = createThread({ ...validInput, slug: "   " });
      expect(thread.slug).toBeUndefined();
    });

    it("should trim slug before validation", () => {
      const thread = createThread({ ...validInput, slug: "  my-slug  " });
      expect(thread.slug).toBe("my-slug");
    });

    it("should accept slug at exactly max length", () => {
      const maxSlug = "a".repeat(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH);
      const thread = createThread({ ...validInput, slug: maxSlug });
      expect(thread.slug).toHaveLength(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH);
    });

    it("should throw when slug exceeds max length", () => {
      const longSlug = "a".repeat(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH + 1);
      expect(() => createThread({ ...validInput, slug: longSlug })).toThrow(CommsValidationError);
    });

    // --- Timestamp consistency ---

    it("should set createdAt and updatedAt to the same value", () => {
      const thread = createThread(validInput);
      expect(thread.createdAt.getTime()).toBe(thread.updatedAt.getTime());
    });

    // --- All thread types ---

    it.each(["chat", "support", "forum", "system"] as const)(
      "should accept thread type '%s'",
      (type) => {
        const thread = createThread({ type });
        expect(thread.type).toBe(type);
      },
    );
  });

  describe("updateThread", () => {
    const baseThread = createThread({
      type: "forum",
      title: "Original",
      slug: "original",
    });

    it("should update title", () => {
      const updated = updateThread(baseThread, { title: "Updated Title" });
      expect(updated.title).toBe("Updated Title");
    });

    it("should update slug", () => {
      const updated = updateThread(baseThread, { slug: "updated-slug" });
      expect(updated.slug).toBe("updated-slug");
    });

    it("should update notification policy", () => {
      const updated = updateThread(baseThread, { notificationPolicy: "none" });
      expect(updated.notificationPolicy).toBe("none");
    });

    it("should clear title by setting to null", () => {
      const updated = updateThread(baseThread, { title: null });
      expect(updated.title).toBeUndefined();
    });

    it("should clear slug by setting to null", () => {
      const updated = updateThread(baseThread, { slug: null });
      expect(updated.slug).toBeUndefined();
    });

    it("should preserve unchanged fields", () => {
      const updated = updateThread(baseThread, { title: "New" });
      expect(updated.type).toBe(baseThread.type);
      expect(updated.state).toBe(baseThread.state);
      expect(updated.slug).toBe(baseThread.slug);
    });

    it("should throw for invalid slug on update", () => {
      expect(() => updateThread(baseThread, { slug: "BAD SLUG" })).toThrow(CommsValidationError);
    });
  });

  describe("transitionThreadState", () => {
    it("should transition from active to archived", () => {
      const thread = createThread(validInput);
      const archived = transitionThreadState(thread, "archived");
      expect(archived.state).toBe("archived");
    });

    it("should transition from active to closed", () => {
      const thread = createThread(validInput);
      const closed = transitionThreadState(thread, "closed");
      expect(closed.state).toBe("closed");
    });

    it("should transition from archived to active", () => {
      const thread = createThread(validInput);
      const archived = transitionThreadState(thread, "archived");
      const reactivated = transitionThreadState(archived, "active");
      expect(reactivated.state).toBe("active");
    });

    it("should transition from archived to closed", () => {
      const thread = createThread(validInput);
      const archived = transitionThreadState(thread, "archived");
      const closed = transitionThreadState(archived, "closed");
      expect(closed.state).toBe("closed");
    });

    it("should throw when transitioning from closed (terminal)", () => {
      const thread = createThread(validInput);
      const closed = transitionThreadState(thread, "closed");
      expect(() => transitionThreadState(closed, "active")).toThrow(CommsDomainError);
    });

    it("should throw for invalid transition from active to active", () => {
      const thread = createThread(validInput);
      expect(() => transitionThreadState(thread, "active")).toThrow(CommsDomainError);
    });

    it("should update updatedAt on transition", () => {
      const thread = createThread(validInput);
      const archived = transitionThreadState(thread, "archived");
      expect(archived.updatedAt.getTime()).toBeGreaterThanOrEqual(thread.updatedAt.getTime());
    });
  });
});
