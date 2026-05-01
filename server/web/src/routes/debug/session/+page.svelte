<script lang="ts">
  import { dev } from "$app/environment";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import BugIcon from "@lucide/svelte/icons/bug";
  import * as m from "$lib/paraglide/messages.js";
  import RefreshCwIcon from "@lucide/svelte/icons/refresh-cw";

  let { data } = $props();

  let clientSession: unknown = $state(null);
  let clientSessionError: string | null = $state(null);
  let clientToken: string | null = $state(null);
  let clientTokenError: string | null = $state(null);
  let decodedJwt: unknown = $state(null);
  let loading = $state(false);

  function decodeJwtPayload(token: string): unknown {
    try {
      const parts = token.split(".");
      if (parts.length !== 3) return { error: "Not a valid JWT (expected 3 parts)" };
      const payload = atob(parts[1].replace(/-/g, "+").replace(/_/g, "/"));
      return JSON.parse(payload);
    } catch {
      return { error: "Failed to decode JWT payload" };
    }
  }

  async function fetchClientData() {
    loading = true;
    clientSessionError = null;
    clientTokenError = null;

    try {
      const res = await authClient.getSession();
      clientSession = res.data ?? null;
      if (res.error) {
        clientSessionError = JSON.stringify(res.error, null, 2);
      }
    } catch (e) {
      clientSessionError = e instanceof Error ? e.message : String(e);
    }

    try {
      const tokenRes = await fetch("/api/auth/token", {
        method: "GET",
        credentials: "include",
      });
      if (tokenRes.ok) {
        const json = await tokenRes.json();
        clientToken = json?.token ?? null;
        if (clientToken) {
          decodedJwt = decodeJwtPayload(clientToken);
        }
      } else {
        clientTokenError = `HTTP ${tokenRes.status}: ${tokenRes.statusText}`;
      }
    } catch (e) {
      clientTokenError = e instanceof Error ? e.message : String(e);
    }

    loading = false;
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_session_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_debug_session_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
  <meta
    property="og:title"
    content="{m.webclient_debug_session_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_debug_session_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

{#if !dev}
  <p>{m.webclient_debug_health_not_available()}</p>
{:else}
  <div class="mx-auto flex max-w-4xl flex-col gap-6 p-6">
    <div class="flex items-center justify-between">
      <div class="flex items-center gap-3">
        <div class="bg-muted flex size-10 items-center justify-center rounded-full">
          <BugIcon class="text-muted-foreground size-5" />
        </div>
        <div>
          <h1 class="text-2xl font-bold">{m.webclient_debug_session_title()}</h1>
          <p class="text-muted-foreground text-sm">
            {m.webclient_debug_session_dev_description()}
          </p>
        </div>
      </div>
      <Button
        variant="outline"
        size="sm"
        onclick={fetchClientData}
        disabled={loading}
      >
        <RefreshCwIcon class="mr-2 size-4" />
        {loading ? m.common_ui_loading() : m.webclient_debug_session_fetch_client_data()}
      </Button>
    </div>

    <div class="grid gap-6 md:grid-cols-2">
      <!-- Server: Session -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_server_session()}</Card.Title>
          <Card.Description
            >From <code>event.locals.session</code> (via SessionResolver)</Card.Description
          >
        </Card.Header>
        <Card.Content>
          {#if data.debugSession}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground">userId</dt>
                <dd class="font-mono text-xs">{data.debugSession.userId}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">activeOrgId</dt>
                <dd class="font-mono text-xs">
                  {data.debugSession.activeOrganizationId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">activeOrgType</dt>
                <dd>{data.debugSession.activeOrganizationType ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">activeOrgRole</dt>
                <dd>{data.debugSession.activeOrganizationRole ?? "null"}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">emulatedOrgId</dt>
                <dd class="font-mono text-xs">
                  {data.debugSession.emulatedOrganizationId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">emulatedOrgType</dt>
                <dd>{data.debugSession.emulatedOrganizationType ?? "null"}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_session()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Server: User -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_server_user()}</Card.Title>
          <Card.Description
            >From <code>event.locals.user</code> (via SessionResolver)</Card.Description
          >
        </Card.Header>
        <Card.Content>
          {#if data.debugUser}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground">id</dt>
                <dd class="font-mono text-xs">{data.debugUser.id}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">email</dt>
                <dd>{data.debugUser.email}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">name</dt>
                <dd>{data.debugUser.name}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">username</dt>
                <dd>{data.debugUser.username}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">displayUsername</dt>
                <dd>{data.debugUser.displayUsername}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">image</dt>
                <dd>{data.debugUser.image ?? "null"}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_user()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Request Context: Tracing -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_request_tracing()}</Card.Title>
          <Card.Description>
            From <code>event.locals.requestContext</code> (enrichment + auth middleware)
          </Card.Description>
        </Card.Header>
        <Card.Content>
          {#if data.requestContext}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground">traceId</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.traceId}
                >
                  {data.requestContext.traceId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">requestId</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.requestId}
                >
                  {data.requestContext.requestId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">requestPath</dt>
                <dd class="font-mono text-xs">{data.requestContext.requestPath ?? "null"}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_request_context_middleware()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Request Context: Identity & Auth -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_request_identity()}</Card.Title>
          <Card.Description>User identity and auth flags from scope middleware</Card.Description>
        </Card.Header>
        <Card.Content>
          {#if data.requestContext}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isAuthenticated</dt>
                <dd>{data.requestContext.isAuthenticated}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">userId</dt>
                <dd class="font-mono text-xs">{data.requestContext.userId ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">email</dt>
                <dd class="text-xs">{data.requestContext.email ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">username</dt>
                <dd class="text-xs">{data.requestContext.username ?? "null"}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isTrustedService</dt>
                <dd>{data.requestContext.isTrustedService}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isAgentStaff</dt>
                <dd>{data.requestContext.isAgentStaff}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isAgentAdmin</dt>
                <dd>{data.requestContext.isAgentAdmin}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isTargetingStaff</dt>
                <dd>{data.requestContext.isTargetingStaff}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isTargetingAdmin</dt>
                <dd>{data.requestContext.isTargetingAdmin}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_request_context()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Request Context: Organization -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_request_orgs()}</Card.Title>
          <Card.Description>Agent (actual) vs Target (effective) org context</Card.Description>
        </Card.Header>
        <Card.Content>
          {#if data.requestContext}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground font-medium">Agent Org</dt>
                <dd></dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">agentOrgId</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.agentOrgId}
                >
                  {data.requestContext.agentOrgId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">agentOrgName</dt>
                <dd class="text-xs">{data.requestContext.agentOrgName ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">agentOrgType</dt>
                <dd>{data.requestContext.agentOrgType ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">agentOrgRole</dt>
                <dd>{data.requestContext.agentOrgRole ?? "null"}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground font-medium">Target Org</dt>
                <dd></dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">targetOrgId</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.targetOrgId}
                >
                  {data.requestContext.targetOrgId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">targetOrgName</dt>
                <dd class="text-xs">{data.requestContext.targetOrgName ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">targetOrgType</dt>
                <dd>{data.requestContext.targetOrgType ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground pl-2">targetOrgRole</dt>
                <dd>{data.requestContext.targetOrgRole ?? "null"}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isOrgEmulating</dt>
                <dd>{data.requestContext.isOrgEmulating}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isUserImpersonating</dt>
                <dd>{data.requestContext.isUserImpersonating}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">impersonatedBy</dt>
                <dd class="font-mono text-xs">{data.requestContext.impersonatedBy ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">impersonatingEmail</dt>
                <dd class="text-xs">{data.requestContext.impersonatingEmail ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">impersonatingUsername</dt>
                <dd class="text-xs">{data.requestContext.impersonatingUsername ?? "null"}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_request_context()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Request Context: Network & Geo -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_request_network()}</Card.Title>
          <Card.Description>IP resolution, fingerprints, WhoIs enrichment</Card.Description>
        </Card.Header>
        <Card.Content>
          {#if data.requestContext}
            <dl class="space-y-2 text-sm">
              <div class="flex justify-between">
                <dt class="text-muted-foreground">clientIp</dt>
                <dd class="font-mono text-xs">{data.requestContext.clientIp ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">serverFingerprint</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.serverFingerprint}
                >
                  {data.requestContext.serverFingerprint ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">clientFingerprint</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.clientFingerprint}
                >
                  {data.requestContext.clientFingerprint ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">deviceFingerprint</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.deviceFingerprint}
                >
                  {data.requestContext.deviceFingerprint ?? "null"}
                </dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">whoIsHashId</dt>
                <dd
                  class="max-w-48 truncate font-mono text-xs"
                  title={data.requestContext.whoIsHashId}
                >
                  {data.requestContext.whoIsHashId ?? "null"}
                </dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">city</dt>
                <dd>{data.requestContext.city ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">countryCode</dt>
                <dd>{data.requestContext.countryCode ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">subdivisionCode</dt>
                <dd>{data.requestContext.subdivisionCode ?? "null"}</dd>
              </div>
              <Separator />
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isVpn</dt>
                <dd>{data.requestContext.isVpn ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isProxy</dt>
                <dd>{data.requestContext.isProxy ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isTor</dt>
                <dd>{data.requestContext.isTor ?? "null"}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-muted-foreground">isHosting</dt>
                <dd>{data.requestContext.isHosting ?? "null"}</dd>
              </div>
            </dl>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_request_context_middleware()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>

      <!-- Cookies -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_session_cookies()}</Card.Title>
          <Card.Description>{m.webclient_debug_session_cookies_description()}</Card.Description>
        </Card.Header>
        <Card.Content>
          {#if data.cookieNames.length > 0}
            <ul class="space-y-1 text-sm">
              {#each data.cookieNames as name (name)}
                <li class="font-mono text-xs">{name}</li>
              {/each}
            </ul>
          {:else}
            <p class="text-muted-foreground text-sm italic">
              {m.webclient_debug_session_no_cookies()}
            </p>
          {/if}
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Client-side section (full width) -->
    <Card.Root>
      <Card.Header>
        <Card.Title class="text-base">{m.webclient_debug_session_client_auth()}</Card.Title>
        <Card.Description>
          From <code>authClient.getSession()</code> and <code>/api/auth/token</code> — click "Fetch Client
          Data" above
        </Card.Description>
      </Card.Header>
      <Card.Content>
        {#if clientSession || clientSessionError || clientToken || clientTokenError}
          <div class="grid gap-6 md:grid-cols-2">
            <div>
              <h4 class="mb-2 text-sm font-medium">{m.webclient_debug_session_raw_session()}</h4>
              {#if clientSessionError}
                <pre
                  class="bg-destructive/10 text-destructive rounded-md p-3 text-xs">{clientSessionError}</pre>
              {:else if clientSession}
                <pre class="bg-muted max-h-64 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(
                    clientSession,
                    null,
                    2,
                  )}</pre>
              {:else}
                <p class="text-muted-foreground text-sm italic">
                  {m.webclient_debug_session_no_session_returned()}
                </p>
              {/if}
            </div>
            <div>
              <h4 class="mb-2 text-sm font-medium">{m.webclient_debug_session_jwt_claims()}</h4>
              {#if clientTokenError}
                <pre
                  class="bg-destructive/10 text-destructive rounded-md p-3 text-xs">{clientTokenError}</pre>
              {:else if decodedJwt}
                <pre class="bg-muted max-h-64 overflow-auto rounded-md p-3 text-xs">{JSON.stringify(
                    decodedJwt,
                    null,
                    2,
                  )}</pre>
              {:else}
                <p class="text-muted-foreground text-sm italic">
                  {m.webclient_debug_session_no_jwt()}
                </p>
              {/if}
            </div>
          </div>
        {:else}
          <p class="text-muted-foreground text-sm italic">
            {m.webclient_debug_session_click_fetch()}
          </p>
        {/if}
      </Card.Content>
    </Card.Root>

    <!-- Role Audit Note -->
    <Card.Root class="border-info/30 bg-info/5">
      <Card.Header>
        <Card.Title class="text-base">{m.webclient_debug_session_role_audit()}</Card.Title>
      </Card.Header>
      <Card.Content class="space-y-2 text-sm">
        <p>
          BetterAuth has <strong>two separate "role" concepts</strong> that can be confusing:
        </p>
        <dl class="space-y-3">
          <div>
            <dt class="font-medium">
              User-level role <span class="text-muted-foreground font-normal"
                >(user.role column)</span
              >
            </dt>
            <dd class="text-muted-foreground mt-0.5">
              Set by the <code>admin()</code> plugin's <code>defaultRole: "agent"</code>. Stored in
              the <code>user</code> table. <strong>Not exposed</strong> to the frontend — AuthUser type
              does not include it. Visible only in raw BetterAuth API responses (click "Fetch Client Data"
              to see it in the session response).
            </dd>
          </div>
          <div>
            <dt class="font-medium">
              Org-level role
              <span class="text-muted-foreground font-normal"
                >(member.role / session.activeOrganizationRole)</span
              >
            </dt>
            <dd class="text-muted-foreground mt-0.5">
              The actual authorization role — set when a user joins an org. Values:
              <code>owner</code>, <code>officer</code>, <code>agent</code>, <code>auditor</code>.
              Appears in session as <code>activeOrganizationRole</code>. This is what the app uses
              for access control.
            </dd>
          </div>
        </dl>
      </Card.Content>
    </Card.Root>
  </div>
{/if}
