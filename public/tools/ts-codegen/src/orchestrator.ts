// -----------------------------------------------------------------------
// SPDX-License-Identifier: Apache-2.0
// Copyright (c) DCSV
// -----------------------------------------------------------------------

import { runAuthContextEmit } from "./auth-context-emit.js";
import { runAuthScopesEmit } from "./auth-scopes-emit.js";
import { runProtocolAudiencesEmit } from "./protocol-audiences-emit.js";
import { runD2ResultEnvelopeEmit } from "./d2result-envelope-emit.js";
import { runDlqFailureMetadataEmit } from "./dlq-failure-metadata-emit.js";
import { runEncryptionDomainsEmit } from "./encryption-domains-emit.js";
import { runEncryptionFrameEmit } from "./encryption-frame-emit.js";
import { runSealedFrameEmit } from "./encryption-frame-sealed-emit.js";
import { runErrorCategoryEmit } from "./error-category-emit.js";
import {
  runAuthErrorCodesEmit,
  runAuthFailuresEmit,
  runErrorCodesEmit,
  runErrorCodesFactoriesEmit,
} from "./error-codes-emit.js";
import { runErrorCodesRegistryEmit } from "./error-codes-registry-emit.js";
import { runFieldConstraintsEmit } from "./field-constraints-emit.js";
import { runGeoEmit } from "./geo-emitter/index.js";
import { runGrpcTrailersEmit } from "./grpc-trailers-emit.js";
import { runHeadersEmit } from "./headers-emit.js";
import { runJwtClaimsEmit } from "./jwt-claims-emit.js";
import { formatDiagnostic } from "./lib/diagnostics.js";
import { runMqMessagesEmit } from "./mq-messages-emit.js";
import { runOtelMessagingTagsEmit } from "./otel-messaging-tags-emit.js";
import { runProblemDetailsEmit } from "./problem-details-emit.js";
import { runRequestContextEmit } from "./request-context-emit.js";
import { runTkKeysEmit } from "./tk-keys-emit.js";
import { runInputErrorEmit, runTkMessageEmit } from "./wire-shape-emit.js";

/**
 * Top-level orchestrator. Runs every per-topic emitter in dep-graph
 * order. `--force` bypasses the per-spec mtime check and re-emits every
 * artifact unconditionally (used for verification + spec-edit sweeps).
 */
function main(): void {
  const force = process.argv.includes("--force");
  const allDiagnostics = [
    ...runAuthContextEmit(force),
    // request-context extends auth-context — emit auth-context first.
    ...runRequestContextEmit(force),
    ...runAuthScopesEmit(force),
    // Protocol audiences — emits the ProtocolAudiences const-object (d2.internal,
    // d2-edge bare-token aud values) into @dcsv-io/d2-auth-abstractions from
    // contracts/auth-protocol-audiences/protocol-audiences.spec.json. The
    // @d2Audience validator (typespec-decorators) reads the same spec directly.
    // Independent catalog — order within this block does not matter.
    ...runProtocolAudiencesEmit(force),
    // Error category — emits the closed ErrorCategory string-union into
    // @dcsv-io/d2-error-category from contracts/error-category/error-category.spec.json.
    // @dcsv-io/d2-error-codes-registry imports ErrorCategory from this leaf; a future
    // step wires @dcsv-io/d2-result to import it too. Emit before the error-codes
    // blocks that consume the category wire strings.
    ...runErrorCategoryEmit(force),
    ...runAuthErrorCodesEmit(force),
    // auth-failures depends on auth-error-codes constants — emit after.
    ...runAuthFailuresEmit(force),
    // generic error-codes catalog (D2Result error-code constants) — emits
    // independently into @dcsv-io/d2-result.
    ...runErrorCodesEmit(force),
    // generic base factories (the D2Result semantic factories — notFound /
    // unauthorized / ...) — depends on the generic constants; emit after.
    ...runErrorCodesFactoriesEmit(force),
    // merged error-code registry — globs all *-error-codes.spec.json, runs
    // cross-catalog collision check (D2ERC004 / D2ERC005), emits one
    // error-code-registry.g.ts into @dcsv-io/d2-error-codes-registry. Depends on
    // the per-catalog emits having run first (so the per-catalog committed
    // .g.ts files reflect the current spec) but reads the specs directly
    // for its own output — no circular import dependency.
    ...runErrorCodesRegistryEmit(force),
    // headers + jwt-claims + problem-details emit independent catalogs from
    // their own specs.
    ...runHeadersEmit(force),
    ...runJwtClaimsEmit(force),
    ...runProblemDetailsEmit(force),
    // i18n TK keys catalog — emits into @dcsv-io/d2-i18n. Decomposes every key in
    // contracts/messages/en-US.json into a nested const object
    // (TK.<domain>.<category>.<CONSTANT>) mirroring the .NET KeyDecomposer
    // output. Independent of all other emitters — order within this block
    // does not matter.
    ...runTkKeysEmit(force),
    // Wire shapes (TKMessage + InputError) emit independent property-name
    // catalogs from their own specs; consumed by @dcsv-io/d2-result.
    ...runTkMessageEmit(force),
    ...runInputErrorEmit(force),
    // D2Result Shape B envelope (success / data / messages / inputErrors /
    // errorCode / traceId / statusCode) — emits into @dcsv-io/d2-result. Mirrors
    // .NET DcsvIo.D2.Result.D2ResultEnvelopeFieldNames byte-for-byte; the
    // BFF gateway parser reads via these constants instead of hand-rolled
    // string literals.
    ...runD2ResultEnvelopeEmit(force),
    // gRPC trailers — emits into @dcsv-io/d2-grpc-client. Cross-language parity for
    // the d2_error_code / d2_messages / traceId trailer keys.
    ...runGrpcTrailersEmit(force),
    // OTel messaging activity tags — emits into @dcsv-io/d2-telemetry. Closed catalog
    // of OTel semantic-convention attribute names referenced by .NET messaging
    // publisher + consumer; the TS side exposes the same identifiers for any
    // TS messaging instrumentation that needs them.
    ...runOtelMessagingTagsEmit(force),
    // Encryption domains — emits into @dcsv-io/d2-encryption-abstractions. Closed
    // catalog of `audit` / `notifications` / `courier` + `plaintext` sentinel
    // used to identify a keyring across the encryption + messaging surfaces.
    ...runEncryptionDomainsEmit(force),
    // DLQ failure metadata (fields + causes) — emits into
    // @dcsv-io/d2-messaging-abstractions. Consumed by DLQ ops tooling and any TS
    // RabbitMQ subscriber that reads DLQ entries.
    ...runDlqFailureMetadataEmit(force),
    // MQ messages descriptor mirror — emits into @dcsv-io/d2-messaging-abstractions
    // from contracts/mq-messages/mq-messages.spec.json. Mirrors .NET
    // MqMessages + MqMessagesRegistry so the TS RabbitMQ consumer resolves
    // exchange / exchangeType / encryption per message type identically.
    ...runMqMessagesEmit(force),
    // Encryption frame binary layout — emits into @dcsv-io/d2-encryption-abstractions
    // as field-offset constants + byte-length constants. Consumed by ops
    // tooling and any TS reader of the on-wire encryption frame.
    ...runEncryptionFrameEmit(force),
    // SEALED encryption frame binary layout (version 2, the asymmetric
    // ECDH-ES hybrid) — emits into @dcsv-io/d2-encryption-abstractions from the
    // sibling contracts/encryption-frame-sealed/ spec. A separate spec +
    // emitter so the version-1 artifacts stay byte-identical.
    ...runSealedFrameEmit(force),
    // Geo catalogs — emits TS record shapes + branded code types + Zod
    // schemas + closed-set validation tables into @dcsv-io/d2-geo-abstractions from
    // the seven contracts/geo/*.spec.json Tier-2 files. Catalog DATA
    // emission populates @dcsv-io/d2-geo-default's catalog index.
    ...runGeoEmit(force),
    // Field-constraints catalog — emits FieldConstraints const-object +
    // NamePrefix / NameSuffix / BiologicalSex taxonomy enums (branded types +
    // Zod schemas + membership sets) into @dcsv-io/d2-validation-abstractions from
    // contracts/validation/field-constraints.spec.json. Mirrors .NET
    // DcsvIo.D2.Validation.Abstractions byte-for-byte. Independent catalog —
    // order within this block does not matter.
    ...runFieldConstraintsEmit(force),
  ];
  for (const d of allDiagnostics) console.error(formatDiagnostic(d));
  if (allDiagnostics.some((d) => d.severity === "error")) {
    process.exit(1);
  }
}

main();
