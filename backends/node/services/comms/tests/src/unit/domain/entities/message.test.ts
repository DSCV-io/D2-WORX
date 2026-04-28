import { describe, it, expect } from "vitest";
import {
  createMessage,
  editMessage,
  softDeleteMessage,
  CommsValidationError,
  THREAD_CONSTRAINTS,
} from "@d2/comms-domain";

describe("Message", () => {
  const validInput = {
    content: "Hello, world!",
    plainTextContent: "Hello, world!",
    senderUserId: "user-123",
  };

  describe("createMessage", () => {
    it("should create a message with valid input", () => {
      const msg = createMessage(validInput);
      expect(msg.content).toBe("Hello, world!");
      expect(msg.plainTextContent).toBe("Hello, world!");
      expect(msg.senderUserId).toBe("user-123");
      expect(msg.id).toHaveLength(36);
      expect(msg.createdAt).toBeInstanceOf(Date);
      expect(msg.updatedAt).toBeInstanceOf(Date);
    });

    it("should generate unique IDs", () => {
      const msg1 = createMessage(validInput);
      const msg2 = createMessage(validInput);
      expect(msg1.id).not.toBe(msg2.id);
    });

    it("should accept a pre-generated ID", () => {
      const msg = createMessage({ ...validInput, id: "custom-id" });
      expect(msg.id).toBe("custom-id");
    });

    it("should default to markdown content format", () => {
      const msg = createMessage(validInput);
      expect(msg.contentFormat).toBe("markdown");
    });

    it("should default to normal urgency", () => {
      const msg = createMessage(validInput);
      expect(msg.urgency).toBe("normal");
    });

    it("should default channels to empty array", () => {
      const msg = createMessage(validInput);
      expect(msg.channels).toEqual([]);
    });

    it("should default optional fields to undefined", () => {
      const msg = createMessage(validInput);
      expect(msg.threadId).toBeUndefined();
      expect(msg.parentMessageId).toBeUndefined();
      expect(msg.senderContactId).toBeUndefined();
      expect(msg.senderService).toBeUndefined();
      expect(msg.title).toBeUndefined();
      expect(msg.relatedEntityId).toBeUndefined();
      expect(msg.relatedEntityType).toBeUndefined();
      expect(msg.metadata).toBeUndefined();
      expect(msg.editedAt).toBeUndefined();
      expect(msg.deletedAt).toBeUndefined();
    });

    it("should accept all optional fields", () => {
      const msg = createMessage({
        ...validInput,
        threadId: "thread-1",
        parentMessageId: "msg-parent",
        senderContactId: "contact-1",
        senderService: "auth",
        title: "Test Title",
        contentFormat: "html",
        channels: ["email"],
        urgency: "urgent",
        relatedEntityId: "entity-1",
        relatedEntityType: "invoice",
        metadata: { key: "value" },
      });
      expect(msg.threadId).toBe("thread-1");
      expect(msg.parentMessageId).toBe("msg-parent");
      expect(msg.title).toBe("Test Title");
      expect(msg.contentFormat).toBe("html");
      expect(msg.channels).toEqual(["email"]);
      expect(msg.urgency).toBe("urgent");
      expect(msg.relatedEntityId).toBe("entity-1");
      expect(msg.relatedEntityType).toBe("invoice");
      expect(msg.metadata).toEqual({ key: "value" });
    });

    it("should clean and trim content", () => {
      const msg = createMessage({ ...validInput, content: "  hello  " });
      expect(msg.content).toBe("hello");
    });

    it("should clean and trim plainTextContent", () => {
      const msg = createMessage({ ...validInput, plainTextContent: "  plain text  " });
      expect(msg.plainTextContent).toBe("plain text");
    });

    it("should clean and trim title", () => {
      const msg = createMessage({ ...validInput, title: "  My Title  " });
      expect(msg.title).toBe("My Title");
    });

    // --- Validation errors ---

    it("should throw when content is empty", () => {
      expect(() => createMessage({ ...validInput, content: "" })).toThrow(CommsValidationError);
    });

    it("should throw when content is whitespace only", () => {
      expect(() => createMessage({ ...validInput, content: "   " })).toThrow(CommsValidationError);
    });

    it("should throw when plainTextContent is empty", () => {
      expect(() => createMessage({ ...validInput, plainTextContent: "" })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when plainTextContent is whitespace only", () => {
      expect(() => createMessage({ ...validInput, plainTextContent: "   " })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when content exceeds max length", () => {
      const longContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH + 1);
      expect(() => createMessage({ ...validInput, content: longContent })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when plainTextContent exceeds max length", () => {
      const longContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH + 1);
      expect(() => createMessage({ ...validInput, plainTextContent: longContent })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when title exceeds max length", () => {
      const longTitle = "x".repeat(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH + 1);
      expect(() => createMessage({ ...validInput, title: longTitle })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw when no sender is provided", () => {
      expect(() =>
        createMessage({
          content: "test",
          plainTextContent: "test",
        }),
      ).toThrow(CommsValidationError);
    });

    it("should accept senderContactId as sole sender", () => {
      const msg = createMessage({
        content: "test",
        plainTextContent: "test",
        senderContactId: "contact-1",
      });
      expect(msg.senderContactId).toBe("contact-1");
    });

    it("should accept senderService as sole sender", () => {
      const msg = createMessage({
        content: "test",
        plainTextContent: "test",
        senderService: "billing",
      });
      expect(msg.senderService).toBe("billing");
    });

    it("should throw for invalid contentFormat", () => {
      expect(() => createMessage({ ...validInput, contentFormat: "rtf" as never })).toThrow(
        CommsValidationError,
      );
    });

    it("should throw for invalid urgency", () => {
      expect(() => createMessage({ ...validInput, urgency: "critical" as never })).toThrow(
        CommsValidationError,
      );
    });

    // --- Boundary conditions ---

    it("should accept content at exactly max length", () => {
      const maxContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH);
      const msg = createMessage({
        ...validInput,
        content: maxContent,
        plainTextContent: maxContent,
      });
      expect(msg.content).toHaveLength(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH);
    });

    it("should accept plainTextContent at exactly max length", () => {
      const maxContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH);
      const msg = createMessage({ ...validInput, plainTextContent: maxContent });
      expect(msg.plainTextContent).toHaveLength(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH);
    });

    it("should accept title at exactly max length", () => {
      const maxTitle = "x".repeat(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH);
      const msg = createMessage({ ...validInput, title: maxTitle });
      expect(msg.title).toHaveLength(THREAD_CONSTRAINTS.MAX_TITLE_LENGTH);
    });

    // --- Null/undefined handling ---

    it("should treat explicitly null optional fields same as omitted", () => {
      const msg = createMessage({
        ...validInput,
        threadId: null,
        parentMessageId: null,
        title: null,
        relatedEntityId: null,
        relatedEntityType: null,
        metadata: null,
      });
      expect(msg.threadId).toBeNull();
      expect(msg.parentMessageId).toBeNull();
      expect(msg.title).toBeUndefined();
      expect(msg.relatedEntityId).toBeNull();
      expect(msg.relatedEntityType).toBeNull();
      expect(msg.metadata).toBeNull();
    });

    // --- Multiple sender combinations ---

    it("should accept all three senders simultaneously", () => {
      const msg = createMessage({
        content: "test",
        plainTextContent: "test",
        senderUserId: "user-1",
        senderContactId: "contact-1",
        senderService: "billing",
      });
      expect(msg.senderUserId).toBe("user-1");
      expect(msg.senderContactId).toBe("contact-1");
      expect(msg.senderService).toBe("billing");
    });

    // --- Timestamp consistency ---

    it("should set createdAt and updatedAt to the same value", () => {
      const msg = createMessage(validInput);
      expect(msg.createdAt.getTime()).toBe(msg.updatedAt.getTime());
    });
  });

  describe("editMessage", () => {
    const baseMessage = createMessage(validInput);

    it("should update content and plainTextContent", () => {
      const edited = editMessage(baseMessage, "Updated content", "Updated plain text");
      expect(edited.content).toBe("Updated content");
      expect(edited.plainTextContent).toBe("Updated plain text");
    });

    it("should set editedAt", () => {
      const edited = editMessage(baseMessage, "New content", "New plain text");
      expect(edited.editedAt).toBeInstanceOf(Date);
      expect(edited.editedAt).not.toBeNull();
    });

    it("should update updatedAt", () => {
      const edited = editMessage(baseMessage, "New content", "New plain text");
      expect(edited.updatedAt.getTime()).toBeGreaterThanOrEqual(baseMessage.updatedAt.getTime());
    });

    it("should preserve other fields", () => {
      const edited = editMessage(baseMessage, "New content", "New plain text");
      expect(edited.id).toBe(baseMessage.id);
      expect(edited.senderUserId).toBe(baseMessage.senderUserId);
      expect(edited.contentFormat).toBe(baseMessage.contentFormat);
      expect(edited.createdAt).toBe(baseMessage.createdAt);
    });

    it("should throw when new content is empty", () => {
      expect(() => editMessage(baseMessage, "", "plain")).toThrow(CommsValidationError);
    });

    it("should throw when new plainTextContent is empty", () => {
      expect(() => editMessage(baseMessage, "content", "")).toThrow(CommsValidationError);
    });

    it("should throw when new content exceeds max length", () => {
      const longContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH + 1);
      expect(() => editMessage(baseMessage, longContent, "plain")).toThrow(CommsValidationError);
    });

    it("should throw when new plainTextContent exceeds max length", () => {
      const longContent = "x".repeat(THREAD_CONSTRAINTS.MAX_MESSAGE_LENGTH + 1);
      expect(() => editMessage(baseMessage, "content", longContent)).toThrow(CommsValidationError);
    });

    it("should clean and trim content on edit", () => {
      const edited = editMessage(baseMessage, "  trimmed content  ", "  trimmed plain  ");
      expect(edited.content).toBe("trimmed content");
      expect(edited.plainTextContent).toBe("trimmed plain");
    });

    it("should throw when new plainTextContent is whitespace only", () => {
      expect(() => editMessage(baseMessage, "content", "   ")).toThrow(CommsValidationError);
    });
  });

  describe("softDeleteMessage", () => {
    const baseMessage = createMessage(validInput);

    it("should set deletedAt", () => {
      const deleted = softDeleteMessage(baseMessage);
      expect(deleted.deletedAt).toBeInstanceOf(Date);
      expect(deleted.deletedAt).not.toBeNull();
    });

    it("should update updatedAt", () => {
      const deleted = softDeleteMessage(baseMessage);
      expect(deleted.updatedAt.getTime()).toBeGreaterThanOrEqual(baseMessage.updatedAt.getTime());
    });

    it("should preserve content and other fields", () => {
      const deleted = softDeleteMessage(baseMessage);
      expect(deleted.content).toBe(baseMessage.content);
      expect(deleted.id).toBe(baseMessage.id);
      expect(deleted.senderUserId).toBe(baseMessage.senderUserId);
    });
  });
});
