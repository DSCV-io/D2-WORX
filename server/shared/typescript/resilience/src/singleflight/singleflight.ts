// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Coalesces concurrent in-flight async ops by key. Mirrors .NET
 * `Singleflight<TKey, TValue>` — the second concurrent caller for the
 * same key gets the same Promise as the first. After the Promise settles
 * the entry is removed; the next call re-executes the factory.
 */
export class Singleflight<TKey, TValue> {
  private readonly inflight = new Map<TKey, Promise<TValue>>();

  /**
   * Returns the result of `factory()` deduplicated by `key`. Second
   * concurrent call with the same key joins the first call's Promise.
   */
  do(key: TKey, factory: () => Promise<TValue>): Promise<TValue> {
    const existing = this.inflight.get(key);
    if (existing !== undefined) return existing;
    const p = (async () => {
      try {
        return await factory();
      } finally {
        this.inflight.delete(key);
      }
    })();
    this.inflight.set(key, p);
    return p;
  }

  /** Number of in-flight ops — for tests + observability. */
  get inflightCount(): number {
    return this.inflight.size;
  }
}
