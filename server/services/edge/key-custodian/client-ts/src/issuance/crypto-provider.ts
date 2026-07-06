// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// reflect-metadata MUST load before @peculiar/x509 (its tsyringe DI requires the
// reflect polyfill at import time — the documented @peculiar/x509 usage). This
// module is the package's @peculiar bootstrap: every module touching
// @peculiar/x509 imports THIS module BEFORE any @peculiar/x509 import.
import "reflect-metadata";
import { cryptoProvider } from "@peculiar/x509";

// Node exposes the Web Crypto API globally (globalThis.crypto). Using the global
// engine keeps every key type aligned with @peculiar/x509's DOM-typed surface
// (CryptoKeyPair / CryptoKey), avoiding the node:crypto webcrypto-namespace types.
// Register it as @peculiar's default engine ONCE at module load; the CSR generator
// is also passed the same instance explicitly for deterministic behavior.
const engine: Crypto = globalThis.crypto;
cryptoProvider.set(engine);

/** The Web Crypto engine used for all workload-leaf crypto operations. */
export const workloadCrypto: Crypto = engine;
