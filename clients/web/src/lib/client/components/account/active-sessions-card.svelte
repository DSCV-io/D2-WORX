<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import {
    listMySessions,
    revokeSession,
    revokeOtherSessions,
    type ActiveSessionDTO,
  } from "$lib/client/rest/account-client.js";
  import { D2Result } from "@d2/result";
  import { parseUserAgent } from "$lib/shared/utils/user-agent.js";
  import { formatLocation } from "$lib/shared/utils/format-location.js";
  import PasswordConfirmDialog from "./password-confirm-dialog.svelte";
  import LogOutIcon from "@lucide/svelte/icons/log-out";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import SmartphoneIcon from "@lucide/svelte/icons/smartphone";
  import TabletIcon from "@lucide/svelte/icons/tablet";
  import GlobeIcon from "@lucide/svelte/icons/globe";
  import MapPinIcon from "@lucide/svelte/icons/map-pin";
  import ClockIcon from "@lucide/svelte/icons/clock";
  import { toast } from "svelte-sonner";

  let sessions = $state<ActiveSessionDTO[]>([]);
  let loaded = $state(false);
  let loading = $state(false);
  let errorMessage = $state("");

  let revokeDialogOpen = $state(false);
  let revokeTarget = $state<ActiveSessionDTO | null>(null);
  let revokeOthersOpen = $state(false);

  async function reload() {
    loading = true;
    errorMessage = "";
    const result = await listMySessions();
    loading = false;
    if (!result.success) {
      errorMessage = result.messages?.[0] ?? m.common_errors_unknown();
      sessions = [];
      loaded = true;
      return;
    }
    sessions = result.data?.sessions ?? [];
    loaded = true;
  }

  $effect(() => {
    void reload();
  });

  function openRevokeDialog(s: ActiveSessionDTO) {
    revokeTarget = s;
    revokeDialogOpen = true;
  }

  async function doRevoke(password: string) {
    if (!revokeTarget) {
      return D2Result.fail({ statusCode: 400, messages: ["No session selected."] });
    }
    return revokeSession(revokeTarget.session.token, password);
  }

  async function doRevokeOthers(password: string) {
    return revokeOtherSessions(password);
  }

  function fmtRelative(iso: string): string {
    const ts = new Date(iso).getTime();
    const now = Date.now();
    const diff = Math.max(0, now - ts);
    const mins = Math.floor(diff / 60_000);
    if (mins < 1) return m.common_ui_just_now();
    if (mins === 1) return m.account_sessions_minute_ago();
    if (mins < 60) return m.account_sessions_minutes_ago({ count: mins });
    const hours = Math.floor(mins / 60);
    if (hours === 1) return m.account_sessions_hour_ago();
    if (hours < 24) return m.account_sessions_hours_ago({ count: hours });
    const days = Math.floor(hours / 24);
    if (days === 1) return m.account_sessions_day_ago();
    return m.account_sessions_days_ago({ count: days });
  }

  /** Pick a Lucide icon for the device class returned by ua-parser. */
  function deviceIcon(deviceType: string) {
    if (deviceType === "mobile") return SmartphoneIcon;
    if (deviceType === "tablet") return TabletIcon;
    if (deviceType === "desktop" || deviceType === "unknown") return MonitorIcon;
    return GlobeIcon;
  }

  /** Truncate IPv6 in the middle so both ends stay legible. */
  function shortIp(ip: string | undefined): string {
    if (!ip) return m.account_sessions_unknown_ip();
    if (ip.length <= 24) return ip;
    return `${ip.slice(0, 12)}…${ip.slice(-8)}`;
  }

  /** First/last 4 chars of the content-addressable WhoIs hash — quick visual diff between rows. */
  function whoIsStub(id: string | undefined): string | undefined {
    if (!id || id.length < 8) return undefined;
    return `${id.slice(0, 4)}…${id.slice(-4)}`;
  }

  const otherSessionsCount = $derived(sessions.filter((s) => !s.isCurrent).length);
</script>

<Card.Root>
  <Card.Header>
    <Card.Title class="text-base">{m.account_sessions_title()}</Card.Title>
    <Card.Description>{m.account_sessions_description()}</Card.Description>
  </Card.Header>
  <Card.Content class="space-y-4">
    {#if !loaded}
      <ul class="space-y-3">
        {#each [0, 1] as _i (_i)}
          <li class="flex items-start gap-4 rounded-lg border p-4">
            <Skeleton class="size-10 rounded-md" />
            <div class="flex flex-1 flex-col gap-2">
              <Skeleton class="h-5 w-44" />
              <Skeleton class="h-4 w-60" />
            </div>
            <Skeleton class="h-9 w-24 rounded-md" />
          </li>
        {/each}
      </ul>
      <div class="border-t pt-4">
        <Skeleton class="h-9 w-56 rounded-md" />
      </div>
    {:else if errorMessage}
      <p class="text-destructive text-sm">{errorMessage}</p>
    {:else if sessions.length === 0}
      <p class="text-muted-foreground text-sm">{m.account_sessions_empty()}</p>
    {:else}
      <ul class="space-y-3">
        {#each sessions as s (s.session.id)}
          {@const ua = parseUserAgent(s.session.userAgent)}
          {@const loc = formatLocation(s.whoIs)}
          {@const Icon = deviceIcon(ua.deviceType)}
          {@const stub = whoIsStub(s.session.whoIsId)}
          <li
            class={[
              "group rounded-lg border p-4 transition-colors",
              s.isCurrent
                ? "border-success/40 bg-success/5"
                : "hover:border-muted-foreground/30",
            ].join(" ")}
          >
            <div class="flex items-start gap-4">
              <div
                class={[
                  "flex size-10 shrink-0 items-center justify-center rounded-md",
                  s.isCurrent ? "bg-success/10 text-success" : "bg-muted text-muted-foreground",
                ].join(" ")}
              >
                <Icon class="size-5" />
              </div>

              <div class="flex flex-1 flex-col gap-1.5 min-w-0">
                <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                  <span class="text-sm font-semibold">{ua.browser}</span>
                  <span class="text-muted-foreground text-xs"
                    >{m.account_sessions_on_os({ os: ua.os })}</span
                  >
                  {#if stub}
                    <span
                      class="text-muted-foreground/70 font-mono text-[10px]"
                      title={s.session.whoIsId}
                    >
                      ({stub})
                    </span>
                  {/if}
                  {#if s.isCurrent}
                    <Badge variant="success">{m.account_sessions_current_badge()}</Badge>
                  {/if}
                </div>

                <div
                  class="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs"
                >
                  {#if loc}
                    <span class="inline-flex items-center gap-1">
                      <MapPinIcon class="size-3" />
                      {loc}
                    </span>
                  {/if}
                  <span class="font-mono" title={s.session.ipAddress ?? ""}>
                    {shortIp(s.session.ipAddress)}
                  </span>
                  <span class="inline-flex items-center gap-1">
                    <ClockIcon class="size-3" />
                    {fmtRelative(s.session.updatedAt)}
                  </span>
                </div>
              </div>

              {#if !s.isCurrent}
                <!-- Desktop: button hugs the right edge of the row -->
                <Button
                  variant="outline"
                  size="sm"
                  onclick={() => openRevokeDialog(s)}
                  aria-label={m.account_sessions_revoke()}
                  class="hidden sm:inline-flex"
                >
                  <LogOutIcon class="mr-1.5 size-4" />
                  {m.account_sessions_revoke()}
                </Button>
              {/if}
            </div>

            {#if !s.isCurrent}
              <!-- Mobile: button drops below the row, full width — no dead space -->
              <Button
                variant="outline"
                size="sm"
                onclick={() => openRevokeDialog(s)}
                aria-label={m.account_sessions_revoke()}
                class="mt-3 w-full sm:hidden"
              >
                <LogOutIcon class="mr-1.5 size-4" />
                {m.account_sessions_revoke()}
              </Button>
            {/if}
          </li>
        {/each}
      </ul>

      {#if otherSessionsCount > 0}
        <div class="border-t pt-4">
          <Button
            variant="outline"
            size="sm"
            onclick={() => (revokeOthersOpen = true)}
            disabled={loading}
          >
            <LogOutIcon class="mr-1.5 size-4" />
            {m.account_sessions_sign_out_others()}
          </Button>
        </div>
      {/if}
    {/if}
  </Card.Content>
</Card.Root>

<PasswordConfirmDialog
  bind:open={revokeDialogOpen}
  title={m.account_sessions_revoke_dialog_title()}
  description={m.account_sessions_revoke_dialog_description()}
  confirmLabel={m.account_sessions_revoke()}
  confirmVariant="destructive"
  onSubmit={doRevoke}
  onSuccess={async () => {
    toast.success(m.account_sessions_revoked_success());
    await reload();
  }}
/>

<PasswordConfirmDialog
  bind:open={revokeOthersOpen}
  title={m.account_sessions_revoke_others_dialog_title()}
  description={m.account_sessions_revoke_others_dialog_description()}
  confirmLabel={m.account_sessions_sign_out_others()}
  confirmVariant="destructive"
  onSubmit={doRevokeOthers}
  onSuccess={async () => {
    toast.success(m.account_sessions_revoked_others_success());
    await reload();
  }}
/>
