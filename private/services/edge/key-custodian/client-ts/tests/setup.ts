// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Vitest setup — loads the reflect-metadata polyfill before any test module
// imports @peculiar/x509 (whose tsyringe DI requires it at import time). The
// production bootstrap is src/crypto-provider.ts; this covers test files that
// import @peculiar/x509 directly.
import "reflect-metadata";
