import { describe, it, expect } from "vitest";
import { writable } from "svelte/store";
import { flushSync } from "svelte";
import { useAddressLines } from "../address-lines.svelte.js";

function createMockForm(initial: Record<string, string> = {}) {
  const data: Record<string, string> = {
    street1: "",
    street2: "",
    street3: "",
    ...initial,
  };
  return { form: writable(data) };
}

describe("useAddressLines", () => {
  it("starts with showExtraLines=false", () => {
    const form = createMockForm();
    const result = useAddressLines({ form });

    expect(result.showExtraLines).toBe(false);
  });

  it("toggles showExtraLines to true on first call", () => {
    const form = createMockForm();
    const result = useAddressLines({ form });

    flushSync(() => {
      result.toggleExtraLines();
    });

    expect(result.showExtraLines).toBe(true);
  });

  it("toggles back to false on second call", () => {
    const form = createMockForm();
    const result = useAddressLines({ form });

    flushSync(() => {
      result.toggleExtraLines();
    });
    expect(result.showExtraLines).toBe(true);

    flushSync(() => {
      result.toggleExtraLines();
    });
    expect(result.showExtraLines).toBe(false);
  });

  it("clears default extra fields (street2, street3) when hiding", () => {
    const form = createMockForm({ street2: "Apt 4B", street3: "Floor 2" });
    const result = useAddressLines({ form });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    // Show
    flushSync(() => {
      result.toggleExtraLines();
    });
    expect(result.showExtraLines).toBe(true);
    // Values should still be present
    expect(updatedData!.street2).toBe("Apt 4B");

    // Hide — should clear
    flushSync(() => {
      result.toggleExtraLines();
    });
    expect(result.showExtraLines).toBe(false);
    expect(updatedData!.street2).toBe("");
    expect(updatedData!.street3).toBe("");
  });

  it("does not clear fields when showing (only when hiding)", () => {
    const form = createMockForm({ street2: "Apt 4B", street3: "Floor 2" });
    const result = useAddressLines({ form });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    flushSync(() => {
      result.toggleExtraLines();
    });

    expect(result.showExtraLines).toBe(true);
    expect(updatedData!.street2).toBe("Apt 4B");
    expect(updatedData!.street3).toBe("Floor 2");
  });

  it("does not touch street1 when toggling", () => {
    const form = createMockForm({ street1: "123 Main St", street2: "Apt 4B" });
    const result = useAddressLines({ form });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    // Toggle on then off
    flushSync(() => {
      result.toggleExtraLines();
    });
    flushSync(() => {
      result.toggleExtraLines();
    });

    expect(updatedData!.street1).toBe("123 Main St");
  });

  it("uses custom field names when provided", () => {
    const form = createMockForm();
    form.form.set({ line1: "123 Main", line2: "Suite A", line3: "Room 1" });

    const result = useAddressLines({
      form,
      extraFields: ["line2", "line3"],
    });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    // Toggle on then off
    flushSync(() => {
      result.toggleExtraLines();
    });
    flushSync(() => {
      result.toggleExtraLines();
    });

    expect(updatedData!.line2).toBe("");
    expect(updatedData!.line3).toBe("");
    expect(updatedData!.line1).toBe("123 Main");
  });

  it("handles rapid toggling", () => {
    const form = createMockForm();
    const result = useAddressLines({ form });

    flushSync(() => {
      result.toggleExtraLines(); // show
      result.toggleExtraLines(); // hide
      result.toggleExtraLines(); // show
    });

    expect(result.showExtraLines).toBe(true);
  });
});
