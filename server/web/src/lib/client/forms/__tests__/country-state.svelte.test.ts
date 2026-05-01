import { describe, it, expect, vi, afterEach } from "vitest";
import { writable } from "svelte/store";
import { flushSync } from "svelte";
import { useCountryState, type UseCountryStateReturn } from "../country-state.svelte.js";
import type { SubdivisionOption } from "$lib/shared/forms/geo-ref-data.js";

const US_STATES: SubdivisionOption[] = [
  { value: "US-CA", label: "California" },
  { value: "US-NY", label: "New York" },
  { value: "US-TX", label: "Texas" },
];

const CA_PROVINCES: SubdivisionOption[] = [
  { value: "CA-ON", label: "Ontario" },
  { value: "CA-BC", label: "British Columbia" },
];

const SUBDIVISIONS: Record<string, SubdivisionOption[]> = {
  US: US_STATES,
  CA: CA_PROVINCES,
};

function createMockForm(initial: Record<string, string> = {}) {
  const data: Record<string, string> = {
    country: "",
    state: "",
    postalCode: "",
    ...initial,
  };
  const store = writable(data);
  return {
    form: store,
    validate: vi.fn(),
  };
}

/**
 * Create composable inside an $effect.root() so that $effect() works.
 * Returns the composable result + a cleanup function.
 */
function createInRoot(
  ...args: Parameters<typeof useCountryState>
): [UseCountryStateReturn, () => void] {
  let result!: UseCountryStateReturn;
  const cleanup = $effect.root(() => {
    result = useCountryState(...args);
  });
  return [result, cleanup];
}

describe("useCountryState", () => {
  let cleanup: (() => void) | undefined;

  afterEach(() => {
    cleanup?.();
    cleanup = undefined;
  });

  it("starts with empty state options and showState=false", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    expect(result.stateOptions).toEqual([]);
    expect(result.showState).toBe(false);
  });

  it("populates stateOptions when handleCountryChange is called with a valid country", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("US");
    });

    expect(result.stateOptions).toEqual(US_STATES);
    expect(result.showState).toBe(true);
  });

  it("returns different options for different countries", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("CA");
    });

    expect(result.stateOptions).toEqual(CA_PROVINCES);
    expect(result.showState).toBe(true);
  });

  it("clears stateOptions when country has no subdivisions", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    // First set to US
    flushSync(() => {
      result.handleCountryChange("US");
    });
    expect(result.showState).toBe(true);

    // Switch to JP (no subdivisions)
    flushSync(() => {
      result.handleCountryChange("JP");
    });
    expect(result.stateOptions).toEqual([]);
    expect(result.showState).toBe(false);
  });

  it("clears stateOptions when empty string is passed", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("US");
    });
    expect(result.showState).toBe(true);

    flushSync(() => {
      result.handleCountryChange("");
    });
    expect(result.stateOptions).toEqual([]);
    expect(result.showState).toBe(false);
  });

  it("clears the state field on country change", () => {
    const form = createMockForm({ country: "US", state: "US-CA" });
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    flushSync(() => {
      result.handleCountryChange("CA");
    });

    expect(updatedData!.state).toBe("");
  });

  it("revalidates postalCode when it has a value", () => {
    const form = createMockForm({ postalCode: "90210" });
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("US");
    });

    expect(form.validate).toHaveBeenCalledWith("postalCode");
  });

  it("does not revalidate postalCode when it is empty", () => {
    const form = createMockForm({ postalCode: "" });
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("US");
    });

    expect(form.validate).not.toHaveBeenCalled();
  });

  it("uses custom field names when provided", () => {
    const form = createMockForm();
    form.form.set({ location_country: "", location_state: "", zip: "" });

    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({
      form,
      subdivisionsByCountry: SUBDIVISIONS,
      countryField: "location_country",
      stateField: "location_state",
      postalCodeField: "zip",
    });

    let updatedData: Record<string, string> | undefined;
    form.form.subscribe((d) => {
      updatedData = d;
    });

    flushSync(() => {
      result.handleCountryChange("US");
    });

    expect(result.stateOptions).toEqual(US_STATES);
    expect(updatedData!.location_state).toBe("");
  });

  it("hydrates stateOptions from initial country value via $effect", async () => {
    const form = createMockForm({ country: "US" });
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    // $effect runs asynchronously — wait for it
    await new Promise((r) => setTimeout(r, 10));

    expect(result.stateOptions).toEqual(US_STATES);
    expect(result.showState).toBe(true);
  });

  it("handles unknown country in subdivisionsByCountry gracefully", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: SUBDIVISIONS });

    flushSync(() => {
      result.handleCountryChange("ZZ");
    });

    expect(result.stateOptions).toEqual([]);
    expect(result.showState).toBe(false);
  });

  it("handles empty subdivisionsByCountry", () => {
    const form = createMockForm();
    let result: UseCountryStateReturn;
    [result, cleanup] = createInRoot({ form, subdivisionsByCountry: {} });

    flushSync(() => {
      result.handleCountryChange("US");
    });

    expect(result.stateOptions).toEqual([]);
    expect(result.showState).toBe(false);
  });
});
