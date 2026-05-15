// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import type { TKMessage } from "@d2/result";
import type { ITranslator } from "../src/i-translator.js";

describe("ITranslator", () => {
  it("can be implemented by hand-rolled stubs (interface contract)", () => {
    const stub: ITranslator = {
      t: (_locale: string, message: TKMessage) => `[${message.key}]`,
    };
    expect(stub.t("en-US", { key: "TK.x" })).toBe("[TK.x]");
  });
});
