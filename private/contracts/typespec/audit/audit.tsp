// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

// Audit TypeSpec contract — multiproc S2S surface for the stub standalone
// Audit host. Public HTTP is Edge-only (process-kind standalone bridge); this
// process serves gRPC only. Product Audit store / RMQ consumer is OUT OF SCOPE
// here (tracked on the product roadmap).

import "@dcsv-io/d2-typespec-decorators";
import "@typespec/http";
import "@typespec/versioning";

using D2;
using Http;
using TypeSpec.Versioning;

@versioned(DcsvIo.D2.Private.Audit.Versions)
namespace DcsvIo.D2.Private.Audit {
  /** Stability-channel versions for the DcsvIo.D2.Private.Audit wire surface. */
  enum Versions {
    v2alpha: "v2alpha",
  }

  /**
   * Stub multiproc S2S response. Status is a stable marker string for the
   * Edge → Audit gRPC path (NIE handler returns ServiceUnavailable).
   */
  model PingAuditOutput {
    /** Stable stub marker for the multiproc S2S path. */
    @d2Field(1)
    status: string;
  }

  /**
   * Multiproc S2S Ping for the Edge→Audit bridge. Requires
   * `internal.audit.ping` (JWT scope) plus mTLS on the gRPC hop (ADR-0022
   * dual-factor). Not health — health is MapD2DefaultEndpoints only.
   * Public HTTP lives only on Edge via the generated bridge.
   */
  @d2Query
  @d2GrpcMethod("AuditPing", "PingAudit")
  @d2ServedBy("Audit")
  @d2Concern("Ping")
  @route("/api/v1/audit/ping")
  @get
  @d2RequireAnyScope("internal.audit.ping")
  op pingAudit(): PingAuditOutput;
}
