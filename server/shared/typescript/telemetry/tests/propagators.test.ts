// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { buildPropagators } from "../src/propagators.js";

describe("buildPropagators", () => {
  it("returns a CompositePropagator covering W3C trace context + baggage", () => {
    const p = buildPropagators();
    expect(p.fields()).toContain("traceparent");
    expect(p.fields()).toContain("tracestate");
    expect(p.fields()).toContain("baggage");
  });
});
