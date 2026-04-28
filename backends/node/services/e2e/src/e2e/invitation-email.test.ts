import { describe, it, expect, beforeAll, afterAll } from "vitest";
import {
  startContainers,
  stopContainers,
  getAuthPgUrl,
  getCommsPgUrl,
  getGeoPgUrl,
  getRedisUrl,
  getAuthPool,
  getCommsPool,
  getRabbitUrl,
} from "../helpers/containers.js";
import { startGeoService, stopGeoService } from "../helpers/geo-dotnet-service.js";
import {
  startAuthService,
  stopAuthService,
  E2E_AUTH_API_KEY,
  type AuthServiceHandle,
} from "../helpers/auth-service.js";
import {
  startCommsService,
  stopCommsService,
  type CommsServiceHandle,
} from "../helpers/comms-service.js";
import { waitFor } from "../helpers/wait.js";

const GEO_API_KEY = "e2e-test-key";

/**
 * Helper: sign up a user, wait for verification email, verify in DB, sign in.
 * Returns the Bearer session token for authenticated API calls.
 */
async function signUpVerifyAndSignIn(
  authHandle: AuthServiceHandle,
  commsHandle: CommsServiceHandle,
  email: string,
  name: string,
  password = "SecurePass123!@#",
): Promise<{ userId: string; token: string }> {
  const beforeCount = commsHandle.stubEmail.sentCount();

  // Sign up (triggers verification email via RabbitMQ → Comms)
  const signUpRes = await authHandle.auth.api.signUpEmail({
    body: { email, password, name },
  });

  // Wait for verification email
  await waitFor(async () => commsHandle.stubEmail.sentCount() > beforeCount, {
    timeout: 15_000,
    label: `verification email for ${email}`,
  });

  // Manually verify email in DB
  await getAuthPool().query('UPDATE "user" SET email_verified = true WHERE id = $1', [
    signUpRes.user.id,
  ]);

  // Sign in to get session token
  const signInRes = await authHandle.auth.api.signInEmail({
    body: { email, password },
  });
  const token =
    (signInRes as Record<string, unknown>).token ??
    ((signInRes as Record<string, unknown>).session as { token: string })?.token;

  return { userId: signUpRes.user.id, token: token as string };
}

/**
 * Helper: create an org and set it as the active organization for the session.
 * Returns the org ID.
 */
async function createAndActivateOrg(
  authHandle: AuthServiceHandle,
  token: string,
  orgName: string,
  orgSlug: string,
): Promise<string> {
  const headers = new Headers({ Authorization: `Bearer ${token}` });

  const org = await authHandle.auth.api.createOrganization({
    body: { name: orgName, slug: orgSlug },
    headers,
  });

  await authHandle.auth.api.setActiveOrganization({
    body: { organizationId: (org as { id: string }).id },
    headers,
  });

  return (org as { id: string }).id;
}

describe("E2E: Invitation → invitation email delivery", () => {
  let authHandle: AuthServiceHandle;
  let commsHandle: CommsServiceHandle;

  beforeAll(async () => {
    await startContainers();

    const geoAddress = await startGeoService({
      pgUrl: getGeoPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitUrl: getRabbitUrl(),
      apiKey: GEO_API_KEY,
    });

    authHandle = await startAuthService({
      databaseUrl: getAuthPgUrl(),
      redisUrl: getRedisUrl(),
      rabbitMqUrl: getRabbitUrl(),
      geoAddress,
      geoApiKey: GEO_API_KEY,
    });

    commsHandle = await startCommsService({
      databaseUrl: getCommsPgUrl(),
      rabbitMqUrl: getRabbitUrl(),
      geoAddress,
      geoApiKey: GEO_API_KEY,
    });
  }, 180_000);

  afterAll(async () => {
    await stopCommsService();
    await stopAuthService();
    await stopGeoService();
    await stopContainers();
  });

  it("should deliver invitation email to a non-existing user (contactId path)", async () => {
    // 1. Sign up inviter, verify, sign in
    const { token } = await signUpVerifyAndSignIn(
      authHandle,
      commsHandle,
      "inviter-new@example.com",
      "Inviter New",
    );

    // 2. Create org and set active
    await createAndActivateOrg(authHandle, token, "Invite New Org", "invite-new-org");

    const beforeInvitationCount = commsHandle.stubEmail.sentCount();

    // 3. POST invitation for a non-existing user
    const response = await authHandle.app.request("/api/invitations", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-api-key": E2E_AUTH_API_KEY,
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({
        email: "new-invitee@example.com",
        role: "agent",
        firstName: "New",
        lastName: "Invitee",
      }),
    });

    expect(response.status).toBe(201);
    const body = (await response.json()) as { data?: { invitationId: string } };
    expect(body.data?.invitationId).toBeDefined();

    // 4. Wait for invitation email
    await waitFor(async () => commsHandle.stubEmail.sentCount() > beforeInvitationCount, {
      timeout: 15_000,
      label: "invitation email delivery (non-existing user)",
    });

    // 5. Assert email content
    const sentEmail = commsHandle.stubEmail.getLastEmail();
    expect(sentEmail).toBeDefined();
    expect(sentEmail!.to).toBe("new-invitee@example.com");
    expect(sentEmail!.subject.toLowerCase()).toContain("invit");

    // 6. Assert delivery records in comms DB
    // Non-existing user → uses recipientContactId, not recipientUserId
    const requests = await getCommsPool().query(
      "SELECT * FROM delivery_request WHERE recipient_contact_id IS NOT NULL ORDER BY created_at DESC LIMIT 1",
    );
    expect(requests.rows.length).toBeGreaterThanOrEqual(1);

    const requestId = requests.rows[0].id;
    const attempts = await getCommsPool().query(
      "SELECT * FROM delivery_attempt WHERE request_id = $1",
      [requestId],
    );
    expect(attempts.rows.length).toBeGreaterThanOrEqual(1);
    expect(attempts.rows[0].channel).toBe("email");
    expect(attempts.rows[0].status).toBe("sent");
  });

  it("should deliver invitation email to an existing user (ext-key contactId resolution)", async () => {
    // 1. Sign up invitee first (so they have a Geo contact)
    const inviteeEmail = "existing-invitee@example.com";
    const inviteeCountBefore = commsHandle.stubEmail.sentCount();

    await authHandle.auth.api.signUpEmail({
      body: { email: inviteeEmail, password: "SecurePass123!@#", name: "Existing Invitee" },
    });

    // Wait for invitee's verification email
    await waitFor(async () => commsHandle.stubEmail.sentCount() > inviteeCountBefore, {
      timeout: 15_000,
      label: "invitee verification email",
    });

    // 2. Sign up inviter, verify, sign in
    const { token } = await signUpVerifyAndSignIn(
      authHandle,
      commsHandle,
      "inviter-existing@example.com",
      "Inviter Existing",
    );

    // 3. Create org and set active
    await createAndActivateOrg(authHandle, token, "Invite Existing Org", "invite-existing-org");

    const beforeInvitationCount = commsHandle.stubEmail.sentCount();

    // 4. POST invitation for the existing user
    const response = await authHandle.app.request("/api/invitations", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "x-api-key": E2E_AUTH_API_KEY,
        Authorization: `Bearer ${token}`,
      },
      body: JSON.stringify({
        email: inviteeEmail,
        role: "agent",
      }),
    });

    expect(response.status).toBe(201);
    const body = (await response.json()) as { data?: { invitationId: string } };
    expect(body.data?.invitationId).toBeDefined();

    // 5. Wait for invitation email
    await waitFor(async () => commsHandle.stubEmail.sentCount() > beforeInvitationCount, {
      timeout: 15_000,
      label: "invitation email delivery (existing user)",
    });

    // 6. Assert email sent to invitee
    const sentEmail = commsHandle.stubEmail.getLastEmail();
    expect(sentEmail).toBeDefined();
    expect(sentEmail!.to).toBe(inviteeEmail);
    expect(sentEmail!.subject.toLowerCase()).toContain("invit");

    // 7. Assert delivery records — existing user now resolves contactId before calling comms
    const requests = await getCommsPool().query(
      "SELECT * FROM delivery_request WHERE recipient_contact_id IS NOT NULL ORDER BY created_at DESC LIMIT 1",
    );
    expect(requests.rows.length).toBeGreaterThanOrEqual(1);

    const requestId = requests.rows[0].id;
    const attempts = await getCommsPool().query(
      "SELECT * FROM delivery_attempt WHERE request_id = $1",
      [requestId],
    );
    expect(attempts.rows.length).toBeGreaterThanOrEqual(1);
    expect(attempts.rows[0].channel).toBe("email");
    expect(attempts.rows[0].status).toBe("sent");
  });
});
