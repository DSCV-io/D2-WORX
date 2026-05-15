// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { Singleflight } from "../singleflight/singleflight.js";
import type { IResilientLayer } from "./i-resilient-layer.js";

/** Layer wrapping an inner layer with key-based singleflight dedup. */
export class SingleflightLayer implements IResilientLayer {
  private readonly sf = new Singleflight<string, unknown>();

  constructor(private readonly inner: IResilientLayer) {}

  execute<T>(key: string, op: () => Promise<T>): Promise<T> {
    return this.sf.do(key, () => this.inner.execute(key, op)) as Promise<T>;
  }
}
