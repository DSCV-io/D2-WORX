// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Enum-shaped fixture TypeSpec contract.
//
// Exercises every SUPPORTED enum / string-literal-union shape the DTO emitter
// maps to a cross-language enum (member-name string wire form, identical across
// C# / proto / TS):
//   S-1  named enum, bare members          → KeyKind { Rsa, Aes, Secret }
//   S-2  named enum, explicit int values    → Level { Low: 0, Medium: 5, High: 10 }
//   S-3  named string-literal union         → Status { active, inactive, pending }
//   S-3  + [EnumMember] (non-identifier)    → AccountKind { internal, third-party }
//   S-4  inline anonymous string-literal    → inlineState: "draft" | "published" | "archived"
//   S-5  optional enum                      → optionalKind?: KeyKind
//   L-6+ array of a supported enum          → kinds: KeyKind[]
//
// The in-process `enums` op walks every shape on both Input + Output (the
// enum-dedup proof rides the shared name). The gRPC `signWithKind` op carries a
// single REQUIRED enum on its INPUT so the proto `string`-field path + the
// proto→DTO transport-mapper parse (fail-loud ValidationFailed on an unknown
// wire value) are exercised end-to-end.
//
// This is a test fixture, not a real operation.

import "@d2/typespec-decorators";

using D2;

namespace D2.Fixtures;

/** Signing-key kind (bare-member named enum — S-1). */
enum KeyKind {
  Rsa,
  Aes,
  Secret,
}

/** Severity level (explicit-int named enum — S-2; STILL a string wire). */
enum Level {
  Low: 0,
  Medium: 5,
  High: 10,
}

/** Lifecycle status (named string-literal union — S-3). */
union Status {
  active: "active",
  inactive: "inactive",
  pending: "pending",
}

/**
 * Account kind (named string-literal union with a non-identifier literal — S-3
 * + [EnumMember]). `third-party` is not a valid C# identifier → the member is
 * `ThirdParty` carrying [EnumMember(Value = "third-party")].
 */
union AccountKind {
  internal: "internal",
  thirdParty: "third-party",
}

/** Input carrying every supported enum / string-literal-union shape. */
model EnumInput {
  /** Bare-member named enum (S-1). */
  keyKind: KeyKind;

  /** Explicit-int named enum (S-2). */
  level: Level;

  /** Named string-literal union (S-3). */
  status: Status;

  /** Named string-literal union with a non-identifier literal (S-3 + [EnumMember]). */
  accountKind: AccountKind;

  /** Inline anonymous string-literal union → synthetic enum EnumInputInlineState (S-4). */
  inlineState: "draft" | "published" | "archived";

  /** Optional enum (S-5). */
  optionalKind?: KeyKind;

  /** Array of a supported enum (L-6-positive). */
  kinds: KeyKind[];
}

/** Output mirrors the input so the enum walk + dedup runs in both directions. */
model EnumOutput {
  /** Bare-member named enum (S-1). */
  keyKind: KeyKind;

  /** Explicit-int named enum (S-2). */
  level: Level;

  /** Named string-literal union (S-3). */
  status: Status;

  /** Named string-literal union with a non-identifier literal (S-3 + [EnumMember]). */
  accountKind: AccountKind;

  /** Inline anonymous string-literal union → synthetic enum EnumOutputInlineState (S-4). */
  inlineState: "draft" | "published" | "archived";

  /** Optional enum (S-5). */
  optionalKind?: KeyKind;

  /** Array of a supported enum (L-6-positive). */
  kinds: KeyKind[];
}

/**
 * Echo every enum shape back. Fixture operation — exercises the DTO emitter's
 * enum / string-literal-union mapping (C# sibling enum + TS const-object) in
 * both walk directions. In-process only (no gRPC), so it is a pure DTO surface.
 */
@d2Query
@d2InProcess
@d2ServedBy("EnumFixtures")
op enums(input: EnumInput): EnumOutput;

/** gRPC request carrying a single REQUIRED enum (the proto string + parse bridge). */
model SignWithKindInput {
  /** The key identifier selecting the signing key. */
  @d2Field(1)
  kid: string;

  /** The signing-key kind — a proto `string` field on the wire (S-1 enum). */
  @d2Field(2)
  keyKind: KeyKind;
}

/**
 * gRPC response carrying an enum on the OUTPUT (S-1). Exercises the client
 * proto-string -> DTO-enum response parse (the inbound CLIENT analogue of the
 * server request parse): the client mapper parses `key_kind` back to the C# enum,
 * failing loud (400 ValidationFailed) on an unknown wire value — symmetric with
 * the server-side request parse, so the gRPC enum surface is complete (request
 * AND response).
 */
model SignWithKindOutput {
  /** The signature over the payload, base64url-encoded (unpadded). */
  @d2Field(1)
  signature: string;

  /** The signing-key kind echoed back — a proto `string` field parsed back to the C# enum (S-1). */
  @d2Field(2)
  keyKind: KeyKind;
}

/**
 * Sign with a typed key kind. Fixture gRPC operation — exercises the proto
 * `string`-field-for-enum path + the proto→DTO transport mapper that parses the
 * wire string back to the C# enum, failing loud (400 ValidationFailed) on an
 * unknown value (strict, matching the JSON deserialization policy).
 */
@d2Command
@d2ServedBy("EnumFixtures")
@d2GrpcMethod("EnumFixturesSigner", "SignWithKind")
op signWithKind(input: SignWithKindInput): SignWithKindOutput;
