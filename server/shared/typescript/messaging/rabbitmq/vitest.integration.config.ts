// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    // Testcontainer-backed integration suite: replays real .NET-emitted golden
    // messages through a live RabbitMQ broker. Self-manages its own container
    // (Testcontainers) — no coverage gate, generous timeouts for image pull +
    // broker boot.
    include: ["tests/integration/**/*.test.ts"],
    testTimeout: 120_000,
    hookTimeout: 180_000,
  },
});
