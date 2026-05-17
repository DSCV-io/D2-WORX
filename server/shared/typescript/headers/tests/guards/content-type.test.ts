// -----------------------------------------------------------------------
// Copyright (c) DCSV. All rights reserved.
// -----------------------------------------------------------------------

import { describe, expect, it } from "vitest";
import { HttpStatusCode } from "@d2/result";
import { redirectIfAuthenticated } from "../../src/guards/redirect-if-authenticated.js";
import { requireAuth } from "../../src/guards/require-auth.js";
import { requireOrg } from "../../src/guards/require-org.js";
import { requireRole } from "../../src/guards/require-role.js";
import { requireScope } from "../../src/guards/require-scope.js";
import { PROBLEM_DETAILS_CONTENT_TYPE } from "../../src/problem-details.g.js";
import { authenticatedCtx, makeEvent, makeThrowers } from "./helpers.js";

/**
 * RFC 7807 §6.1 SHOULD compliance pin: every guard's rejection response
 * MUST carry `Content-Type: application/problem+json` so ProblemDetails-
 * aware clients can distinguish the envelope from a plain JSON error
 * (which carries `application/json`). The wire constant is spec-driven
 * via `@d2/headers`'s `PROBLEM_DETAILS_CONTENT_TYPE`; this test pins
 * that each guard threads it through the `GuardThrowers.throwError`
 * `contentType` parameter on every rejection branch.
 *
 * Closure on the §11.30 / §13.4 dual-binding gap: the spec emits the
 * constant and EVERY consuming site references it. A future regression
 * (guard call forgetting the third arg, or passing a stale literal)
 * fires here.
 */
describe("guards set Content-Type: application/problem+json on rejections", () => {
  it("requireAuth — unauthenticated request", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(undefined);

    expect(() => requireAuth(event, throwers)).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.Unauthorized,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("requireScope — no scopes arg (programmer error)", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(authenticatedCtx());

    expect(() => requireScope(event, throwers)).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.InternalServerError,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("requireScope — insufficient scope", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(authenticatedCtx());

    expect(() => requireScope(event, throwers, "files.read")).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.Forbidden,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("requireRole — no role in org context", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(authenticatedCtx({ orgRole: null }));

    expect(() => requireRole(event, throwers)).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.Forbidden,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("requireOrg — no org context", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(authenticatedCtx({ orgId: null }));

    expect(() => requireOrg(event, throwers)).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.Forbidden,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("redirectIfAuthenticated — invalid target (header injection)", () => {
    const { throwers, thrown } = makeThrowers();
    const event = makeEvent(authenticatedCtx());

    expect(() =>
      redirectIfAuthenticated(
        event,
        throwers,
        "https://evil.com\r\nX-Inject: yes",
      ),
    ).toThrow();

    expect(thrown).toHaveLength(1);
    expect(thrown[0]).toMatchObject({
      kind: "error",
      status: HttpStatusCode.InternalServerError,
      contentType: PROBLEM_DETAILS_CONTENT_TYPE,
    });
  });

  it("PROBLEM_DETAILS_CONTENT_TYPE wire value pin", () => {
    // Per-VALUE pin so a future spec drift on the content-type value
    // surfaces here as well as in the dedicated spec-parity test.
    expect(PROBLEM_DETAILS_CONTENT_TYPE).toBe("application/problem+json");
  });
});
