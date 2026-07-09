// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

/**
 * Queue durability / exclusivity pattern. Byte-identical member names to the
 * .NET `D2.Shared.Messaging.QueuePattern` enum so a subscription's declared
 * pattern reads the same on both runtimes.
 */
export enum QueuePattern {
  /** Commands / requests delivered to one consumer in the fleet. */
  CompetingConsumer = "CompetingConsumer",
  /** Persistent events — survives restart, multiple consumers OK. */
  DurableShared = "DurableShared",
  /**
   * Per-instance broadcast (cache invalidation, keyring refresh) — every
   * replica gets every message. The consumer host auto-suffixes the queue
   * name with a per-process token to avoid the broker's exclusive-queue lock
   * collision in a multi-replica deployment.
   */
  FanoutExclusiveAutoDelete = "FanoutExclusiveAutoDelete",
}

/** The broker-level durability flags a pattern maps to. */
export interface QueueFlags {
  readonly durable: boolean;
  readonly exclusive: boolean;
  readonly autoDelete: boolean;
}

/**
 * Maps a {@link QueuePattern} to its broker-level durability flags — the exact
 * mirror of the .NET `DefaultTopologyDeclarer.QueueFlagsFor` switch.
 */
export function queueFlagsFor(pattern: QueuePattern): QueueFlags {
  switch (pattern) {
    case QueuePattern.CompetingConsumer:
    case QueuePattern.DurableShared:
      return { durable: true, exclusive: false, autoDelete: false };
    case QueuePattern.FanoutExclusiveAutoDelete:
      return { durable: false, exclusive: true, autoDelete: true };
    default:
      throw new Error(`Unknown queue pattern: ${String(pattern)}`);
  }
}
