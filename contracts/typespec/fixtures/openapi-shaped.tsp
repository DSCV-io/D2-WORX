// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// OpenAPI-shaped fixture TypeSpec contract.
//
// Exercises the OpenAPI x-d2-* extension emitter against the genuine stock
// @typespec/openapi3 emitter (getOpenAPI3). Two @service namespaces in one
// compile drive both record arms:
//
//   1. OpenApiFixtures (unversioned) — proves the base path + x-d2-* injection:
//        - sign         : all four policy extensions (any-scope + tier + csrf
//                         + audience) → x-d2-scope/tier/csrf/audience all present.
//        - signMinimal  : only the required auth intent (any-scope), NO tier /
//                         csrf / audience → adversarial ABSENCE: x-d2-scope only.
//        - audit        : @d2RequireAllScopes → x-d2-scope { mode: "all", ... }.
//        - health       : @d2Harmless → x-d2-scope { mode: "harmless" }.
//
//   2. OpenApiVersionedFixtures (@versioned, 2 versions) — proves the per-version
//      file fan-out is REAL (one OpenAPI document per version, not vacuously
//      single-document): `report` exists from v1; `exportReport` is @added in v2
//      so the v1 and v2 documents genuinely differ.
//
// Scopes / audiences are real declared values (contracts/auth-scopes +
// contracts/auth-audiences) so the @d2* decorator validators pass. NOT KC's real
// operations — a fixture mirroring routed signing/reporting shapes for the
// purpose of exercising the OpenAPI emitter.

import "@d2/typespec-decorators";
import "@typespec/http";
import "@typespec/versioning";

using D2;
using Http;
using TypeSpec.Versioning;

// ===========================================================================
// Unversioned service — base x-d2-* injection + adversarial absence + scope arms.
// ===========================================================================

@service(#{ title: "OpenApi Fixtures" })
namespace OpenApiFixtures {
  /** Input for a sign operation: a key identifier and the payload bytes to sign. */
  model OpenApiSignInput {
    /** The key identifier selecting the signing key. */
    kid: string;

    /** The payload to sign. Redacted in structured logs (secret material). */
    @d2Redact
    payload: bytes;
  }

  /** Output of a sign operation: the base64url-encoded signature. */
  model OpenApiSignOutput {
    /** The signature over the payload, base64url-encoded (unpadded). */
    signature: string;
  }

  /** Input for an audit lookup: the audit record identifier. */
  model OpenApiAuditInput {
    /** The audit record identifier to fetch. */
    auditId: string;
  }

  /** Output of an audit lookup: the resolved audit detail. */
  model OpenApiAuditOutput {
    /** The audit detail payload. */
    detail: string;
  }

  /** Output of a health check: a liveness flag. */
  model OpenApiHealthOutput {
    /** Whether the service is live. */
    live: boolean;
  }

  /**
   * Sign the payload — carries ALL FOUR policy decorators so every x-d2-*
   * extension is exercised (x-d2-scope any + x-d2-tier + x-d2-csrf + x-d2-audience).
   */
  @d2Command
  @d2ServedBy("OpenApiFixtures")
  @d2InProcess
  @route("/v1/openapi/sign")
  @post
  @d2RequireAnyScope("self.write")
  @d2RateLimitTier("Standard")
  @d2Csrf("exempt")
  @d2Audience("d2-edge")
  op sign(input: OpenApiSignInput): OpenApiSignOutput;

  /**
   * Sign with ONLY the required auth intent — no tier / csrf / audience. Proves
   * the three extensions are ABSENT when their decorators are absent (extensions
   * are not vacuously stamped).
   */
  @d2Command
  @d2ServedBy("OpenApiFixtures")
  @d2InProcess
  @route("/v1/openapi/sign-minimal")
  @post
  @d2RequireAnyScope("self.write")
  op signMinimal(input: OpenApiSignInput): OpenApiSignOutput;

  /** Fetch an audit record — @d2RequireAllScopes → x-d2-scope { mode: "all" }. */
  @d2Query
  @d2ServedBy("OpenApiFixtures")
  @d2InProcess
  @route("/v1/openapi/audit")
  @get
  @d2RequireAllScopes("self.read", "auth.password.change")
  op audit(input: OpenApiAuditInput): OpenApiAuditOutput;

  /** Health check — @d2Harmless → x-d2-scope { mode: "harmless" }. */
  @d2Query
  @d2ServedBy("OpenApiFixtures")
  @d2InProcess
  @route("/v1/openapi/health")
  @get
  @d2Harmless
  op health(): OpenApiHealthOutput;
}

// ===========================================================================
// Versioned service — proves the per-version OpenAPI document fan-out is real.
// ===========================================================================

@service(#{ title: "OpenApi Versioned Fixtures" })
@versioned(OpenApiVersionedFixtures.OpenApiVersions)
namespace OpenApiVersionedFixtures {
  /** Versions enum driving the @versioned service (two versions). */
  enum OpenApiVersions {
    /** Initial version. */
    v1: "1.0",

    /** Second version — adds exportReport. */
    v2: "2.0",
  }

  /** Input for a report fetch: the report identifier. */
  model OpenApiReportInput {
    /** The report identifier. */
    reportId: string;
  }

  /** Output of a report fetch: the report body. */
  model OpenApiReportOutput {
    /** The rendered report body. */
    body: string;
  }

  /** Output of a report export: the export location. */
  model OpenApiExportOutput {
    /** The location of the exported report artifact. */
    location: string;
  }

  /** Fetch a report — present from v1; carries a tier so x-d2-tier is exercised per version. */
  @d2Query
  @d2ServedBy("OpenApiVersionedFixtures")
  @d2InProcess
  @route("/v1/openapi/report")
  @get
  @d2RequireAnyScope("self.read")
  @d2RateLimitTier("Standard")
  op report(input: OpenApiReportInput): OpenApiReportOutput;

  /**
   * Export a report — @added in v2 so the v1 and v2 OpenAPI documents genuinely
   * differ (the per-version fan-out is proven non-vacuous: this path is in v2 only).
   */
  @added(OpenApiVersionedFixtures.OpenApiVersions.v2)
  @d2Command
  @d2ServedBy("OpenApiVersionedFixtures")
  @d2InProcess
  @route("/v1/openapi/report/export")
  @post
  @d2RequireAnyScope("self.write")
  op exportReport(input: OpenApiReportInput): OpenApiExportOutput;
}
