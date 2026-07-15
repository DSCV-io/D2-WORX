// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { clean } from "../src/clean.js";

describe("clean — happy path", () => {
  it("applies cleaner to every element", () => {
    expect(clean([1, 2, 3], (n) => n * 2)).toEqual([2, 4, 6]);
  });

  it("drops nulls returned by cleaner (default RemoveNulls)", () => {
    expect(clean([1, 2, 3, 4], (n) => (n % 2 === 0 ? n : null))).toEqual([
      2, 4,
    ]);
  });

  it("drops undefined returned by cleaner", () => {
    expect(clean([1, 2, 3], (n) => (n === 2 ? undefined : n))).toEqual([1, 3]);
  });

  it("preserves cleaned values verbatim", () => {
    expect(clean(["A", "B"], (s) => s.toLowerCase())).toEqual(["a", "b"]);
  });
});

describe("clean — null / undefined input", () => {
  it("null input → null when ReturnNull (default)", () => {
    expect(clean<number>(null, (n) => n)).toBeNull();
  });

  it("undefined input → null when ReturnNull (default)", () => {
    expect(clean<number>(undefined, (n) => n)).toBeNull();
  });

  it("null input → empty array when ReturnEmpty", () => {
    expect(
      clean<number>(null, (n) => n, { enumEmptyBehavior: "ReturnEmpty" }),
    ).toEqual([]);
  });

  it("null input → throws when Throw", () => {
    expect(() =>
      clean<number>(null, (n) => n, { enumEmptyBehavior: "Throw" }),
    ).toThrow(RangeError);
  });
});

describe("clean — empty input", () => {
  it("empty array → null by default", () => {
    expect(clean([], (n: number) => n)).toBeNull();
  });

  it("empty array → empty array when ReturnEmpty", () => {
    expect(
      clean([], (n: number) => n, { enumEmptyBehavior: "ReturnEmpty" }),
    ).toEqual([]);
  });

  it("empty array → throws when Throw", () => {
    expect(() =>
      clean([], (n: number) => n, { enumEmptyBehavior: "Throw" }),
    ).toThrow(RangeError);
  });
});

describe("clean — all-cleaned-to-null", () => {
  it("all cleaner returns null → null when ReturnNull (default)", () => {
    expect(clean([1, 2, 3], () => null)).toBeNull();
  });

  it("all cleaner returns null → empty array when ReturnEmpty", () => {
    expect(
      clean([1, 2, 3], () => null, { enumEmptyBehavior: "ReturnEmpty" }),
    ).toEqual([]);
  });

  it("all cleaner returns null → throws when Throw", () => {
    expect(() =>
      clean([1, 2, 3], () => null, { enumEmptyBehavior: "Throw" }),
    ).toThrow(RangeError);
  });
});

describe("clean — ThrowOnNull behavior", () => {
  it("any cleaner result of null throws when ThrowOnNull", () => {
    expect(() =>
      clean([1, 2, 3], (n) => (n === 2 ? null : n), {
        valueNullBehavior: "ThrowOnNull",
      }),
    ).toThrow(RangeError);
  });

  it("undefined cleaner result also throws when ThrowOnNull", () => {
    expect(() =>
      clean([1], () => undefined, { valueNullBehavior: "ThrowOnNull" }),
    ).toThrow(RangeError);
  });

  it("ThrowOnNull does NOT fire when no cleaner returns null", () => {
    expect(
      clean([1, 2, 3], (n) => n, { valueNullBehavior: "ThrowOnNull" }),
    ).toEqual([1, 2, 3]);
  });
});

describe("clean — mixed input shapes", () => {
  it("nulls inside the input are still passed to the cleaner", () => {
    // The cleaner OWNS null handling for elements; clean() only handles
    // null INPUTS to the function as a whole. Cross-language-parity
    // carve-out per rules.md §6.15: this test models a .NET nullable
    // value-type element sequence (`int?[]`) where `null` is the wire
    // sentinel; the `clean()` parity contract preserves the .NET shape.
    type N = number | null;
    const items: N[] = [1, null, 2];
    expect(clean<N>(items, (n) => (n === null ? null : n * 10))).toEqual<N[]>([
      10, 20,
    ]);
  });

  it("mixed empty / whitespace / non-empty strings pass through cleaner", () => {
    const items = ["a", "", "  ", "b"];
    expect(
      clean(items, (s) => (s.trim().length === 0 ? null : s.trim())),
    ).toEqual(["a", "b"]);
  });

  it("oversized input — 10K elements", () => {
    const items = Array.from({ length: 10_000 }, (_, i) => i);
    const result = clean(items, (n) => (n % 2 === 0 ? n : null));
    expect(result).not.toBeNull();
    expect(result!.length).toBe(5_000);
    expect(result![0]).toBe(0);
    expect(result![result!.length - 1]).toBe(9_998);
  });
});

describe("clean — Iterable parity (.NET IEnumerable<T>)", () => {
  it("accepts a Set", () => {
    const items = new Set([1, 2, 3, 4]);
    expect(clean(items, (n) => (n % 2 === 0 ? n : null))).toEqual([2, 4]);
  });

  it("accepts Map values (Iterable<V>)", () => {
    const m = new Map<string, number>([
      ["a", 1],
      ["b", 2],
    ]);
    expect(clean(m.values(), (n) => n * 10)).toEqual([10, 20]);
  });

  it("accepts a generator function", () => {
    function* gen(): Generator<number> {
      yield 1;
      yield 2;
      yield 3;
    }
    expect(clean(gen(), (n) => n + 1)).toEqual([2, 3, 4]);
  });

  it("empty generator → null by default", () => {
    function* gen(): Generator<number> {
      // intentionally empty
    }
    expect(clean(gen(), (n) => n)).toBeNull();
  });
});

describe("clean — cross-type input", () => {
  it("operates on objects", () => {
    interface Item {
      readonly id: string;
      readonly value: number;
    }
    const items: Item[] = [
      { id: "a", value: 1 },
      { id: "b", value: 2 },
      { id: "c", value: 3 },
    ];
    const r = clean(items, (i) => (i.value > 1 ? i : null));
    expect(r).toEqual([
      { id: "b", value: 2 },
      { id: "c", value: 3 },
    ]);
  });

  it("operates on string arrays", () => {
    expect(clean(["A", "BB", "CCC"], (s) => (s.length > 1 ? s : null))).toEqual(
      ["BB", "CCC"],
    );
  });
});

describe("clean — boundary cases", () => {
  it("single-element array, kept", () => {
    expect(clean([1], (n) => n)).toEqual([1]);
  });

  it("single-element array, dropped → null", () => {
    expect(clean([1], () => null)).toBeNull();
  });

  it("ReturnEmpty + ThrowOnNull together — value-null still throws", () => {
    expect(() =>
      clean([1], () => null, {
        enumEmptyBehavior: "ReturnEmpty",
        valueNullBehavior: "ThrowOnNull",
      }),
    ).toThrow(RangeError);
  });
});
