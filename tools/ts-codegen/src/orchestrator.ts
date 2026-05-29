// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { runAuthContextEmit } from "./auth-context-emit.js";
import { runAuthErrorCodesEmit } from "./auth-error-codes-emit.js";
import { runAuthFailuresEmit } from "./auth-failures-emit.js";
import { runAuthScopesEmit } from "./auth-scopes-emit.js";
import { runD2ResultEnvelopeEmit } from "./d2result-envelope-emit.js";
import { runDlqFailureMetadataEmit } from "./dlq-failure-metadata-emit.js";
import { runEncryptionDomainsEmit } from "./encryption-domains-emit.js";
import { runEncryptionFrameEmit } from "./encryption-frame-emit.js";
import { runErrorCodesEmit } from "./error-codes-emit.js";
import { runGeoEmit } from "./geo-emitter/index.js";
import { runGrpcTrailersEmit } from "./grpc-trailers-emit.js";
import { runHeadersEmit } from "./headers-emit.js";
import { runJwtClaimsEmit } from "./jwt-claims-emit.js";
import { formatDiagnostic } from "./lib/diagnostics.js";
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
    ...runAuthErrorCodesEmit(force),
    // auth-failures depends on auth-error-codes constants — emit after.
    ...runAuthFailuresEmit(force),
    // generic error-codes catalog (D2Result error-code constants) — emits
    // independently into @d2/result.
    ...runErrorCodesEmit(force),
    // headers + jwt-claims + problem-details emit independent catalogs from
    // their own specs.
    ...runHeadersEmit(force),
    ...runJwtClaimsEmit(force),
    ...runProblemDetailsEmit(force),
    // i18n TK keys catalog — emits into @d2/i18n. Decomposes every key in
    // contracts/messages/en-US.json into a nested const object
    // (TK.<domain>.<category>.<CONSTANT>) mirroring the .NET KeyDecomposer
    // output. Independent of all other emitters — order within this block
    // does not matter.
    ...runTkKeysEmit(force),
    // Wire shapes (TKMessage + InputError) emit independent property-name
    // catalogs from their own specs; consumed by @d2/result.
    ...runTkMessageEmit(force),
    ...runInputErrorEmit(force),
    // D2Result Shape B envelope (success / data / messages / inputErrors /
    // errorCode / traceId / statusCode) — emits into @d2/result. Mirrors
    // .NET D2.Shared.Result.D2ResultEnvelopeFieldNames byte-for-byte; the
    // BFF gateway parser reads via these constants instead of hand-rolled
    // string literals.
    ...runD2ResultEnvelopeEmit(force),
    // gRPC trailers — emits into @d2/grpc-client. Cross-language parity for
    // the d2_error_code / d2_messages / traceId trailer keys.
    ...runGrpcTrailersEmit(force),
    // OTel messaging activity tags — emits into @d2/telemetry. Closed catalog
    // of OTel semantic-convention attribute names referenced by .NET messaging
    // publisher + consumer; the TS side exposes the same identifiers for any
    // TS messaging instrumentation that needs them.
    ...runOtelMessagingTagsEmit(force),
    // Encryption domains — emits into @d2/encryption-abstractions. Closed
    // catalog of `audit` / `notifications` / `courier` + `plaintext` sentinel
    // used to identify a keyring across the encryption + messaging surfaces.
    ...runEncryptionDomainsEmit(force),
    // DLQ failure metadata (fields + causes) — emits into
    // @d2/messaging-abstractions. Consumed by DLQ ops tooling and any TS
    // RabbitMQ subscriber that reads DLQ entries.
    ...runDlqFailureMetadataEmit(force),
    // Encryption frame binary layout — emits into @d2/encryption-abstractions
    // as field-offset constants + byte-length constants. Consumed by ops
    // tooling and any TS reader of the on-wire encryption frame.
    ...runEncryptionFrameEmit(force),
    // Geo catalogs — emits TS record shapes + branded code types + Zod
    // schemas + closed-set validation tables into @d2/geo-abstractions from
    // the seven contracts/geo/*.spec.json Tier-2 files. Catalog DATA
    // emission populates @d2/geo-default's catalog index.
    ...runGeoEmit(force),
  ];
  for (const d of allDiagnostics) console.error(formatDiagnostic(d));
  if (allDiagnostics.some((d) => d.severity === "error")) {
    process.exit(1);
  }
}

main();
