// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * One layer in a {@link ResilientPipeline}. Wraps the inner async op
 * with its own resilience concern (singleflight / breaker / retry) and
 * delegates downward.
 */
export interface IResilientLayer {
  execute<T>(key: string, op: () => Promise<T>): Promise<T>;
}
