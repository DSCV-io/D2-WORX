// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

// Compile-time type-witness proofs for the publish/encrypt fusion. This file is
// NOT run by vitest (it is `.compile.ts`, not `*.test.ts`); it is type-checked
// by `tsc -p tsconfig.test.json` (the `type-check:test` gate). Each
// `@ts-expect-error` REQUIRES the (single-line) expression that follows to be a
// type error — if the witness ever regresses (an unwired encrypted domain
// becomes publishable, or a mode-branded slot accepts the wrong composer), the
// error disappears and tsc fails on the now-unused directive. The `export`s
// satisfy `noUnusedLocals`. Expressions are kept on one line so Prettier cannot
// move the error off the directive's next line.

import type { IPayloadCrypto, IPayloadSealer } from "@dcsv-io/d2-encryption";

import type {
  DomainCryptoMap,
  PublishableKeyOf,
} from "../src/publishing/domain-crypto-map.js";

// A synthetic catalog: one plaintext message + one sealed (audit) message. The
// production catalog has no sealed message yet, so the witness is proven here.
interface FakeCatalog {
  readonly PlainMsg: { readonly encryption: "plaintext" };
  readonly AuditMsg: { readonly encryption: "payload-fixture-sealed" };
}

declare const auditSealer: IPayloadSealer;
declare const someCrypto: IPayloadCrypto;
declare const bareObject: { readonly nope: true };

type PkAuditWired = PublishableKeyOf<FakeCatalog, { audit: IPayloadSealer }>;
type PkNothingWired = PublishableKeyOf<FakeCatalog, Record<never, never>>;
type Wiring = Partial<DomainCryptoMap>;

// With the audit composer wired, BOTH messages are publishable.
export const _plainWired: PkAuditWired = "PlainMsg";
export const _auditWired: PkAuditWired = "AuditMsg";

// With NOTHING wired, only the plaintext message is publishable.
export const _plainUnwired: PkNothingWired = "PlainMsg";
// @ts-expect-error — AuditMsg's domain is unwired, so it is not publishable.
export const _auditUnwired: PkNothingWired = "AuditMsg";

// The wiring map is mode-branded: the sealed `audit` slot demands a sealer.
export const _goodMap: Wiring = { audit: auditSealer };
// @ts-expect-error — a symmetric crypto is not a sealer; the audit slot rejects it.
export const _cryptoMap: Wiring = { audit: someCrypto };
// @ts-expect-error — a bare object is not a composer for the sealed audit slot.
export const _objectMap: Wiring = { audit: bareObject };
