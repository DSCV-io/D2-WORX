import { describe, it, expect } from "vitest";
import { digitsOnly, alphaOnly, noSpaces, uppercase, maxLength } from "../input-filters.js";

/** Create a mock input event for testing filters. */
function createInputEvent(
  value: string,
  cursorPos?: number,
): Event & { currentTarget: HTMLInputElement } {
  const input = {
    value,
    selectionStart: cursorPos ?? value.length,
    setSelectionRange: (_start: number, _end: number) => {
      // Track the cursor position set by the filter
      input.selectionStart = _start;
    },
  } as unknown as HTMLInputElement;

  return { currentTarget: input } as Event & { currentTarget: HTMLInputElement };
}

describe("digitsOnly", () => {
  it("strips non-digit characters", () => {
    const e = createInputEvent("abc123def456");
    digitsOnly(e);
    expect(e.currentTarget.value).toBe("123456");
  });

  it("keeps all-digit input unchanged", () => {
    const e = createInputEvent("12345");
    digitsOnly(e);
    expect(e.currentTarget.value).toBe("12345");
  });

  it("handles empty string", () => {
    const e = createInputEvent("");
    digitsOnly(e);
    expect(e.currentTarget.value).toBe("");
  });
});

describe("alphaOnly", () => {
  it("strips non-alpha characters", () => {
    const e = createInputEvent("abc123");
    alphaOnly(e);
    expect(e.currentTarget.value).toBe("abc");
  });

  it("keeps both cases", () => {
    const e = createInputEvent("AbCdEf");
    alphaOnly(e);
    expect(e.currentTarget.value).toBe("AbCdEf");
  });
});

describe("noSpaces", () => {
  it("strips all whitespace", () => {
    const e = createInputEvent("hello world");
    noSpaces(e);
    expect(e.currentTarget.value).toBe("helloworld");
  });

  it("strips tabs and newlines", () => {
    const e = createInputEvent("a\tb\nc");
    noSpaces(e);
    expect(e.currentTarget.value).toBe("abc");
  });
});

describe("uppercase", () => {
  it("converts to uppercase", () => {
    const e = createInputEvent("hello");
    uppercase(e);
    expect(e.currentTarget.value).toBe("HELLO");
  });

  it("keeps already uppercase unchanged", () => {
    const e = createInputEvent("ABC");
    uppercase(e);
    expect(e.currentTarget.value).toBe("ABC");
  });
});

describe("maxLength", () => {
  it("truncates to max length", () => {
    const filter = maxLength(5);
    const e = createInputEvent("1234567890");
    filter(e);
    expect(e.currentTarget.value).toBe("12345");
  });

  it("keeps short input unchanged", () => {
    const filter = maxLength(10);
    const e = createInputEvent("abc");
    filter(e);
    expect(e.currentTarget.value).toBe("abc");
  });
});
