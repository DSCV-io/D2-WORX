<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import {
    listMySessions,
    revokeSession,
    revokeOtherSessions,
    type ActiveSessionDTO,
  } from "$lib/client/rest/account-client.js";
  import { D2Result } from "@d2/result";
  import { parseUserAgent } from "$lib/shared/utils/user-agent.js";
  import { formatLocation, locationCountryCode } from "$lib/shared/utils/format-location.js";
  import CopyChip from "./copy-chip.svelte";
  import CountryFlag from "./country-flag.svelte";
  import DeviceIdenticon from "./device-identicon.svelte";
  import PasswordConfirmDialog from "./password-confirm-dialog.svelte";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import LogOutIcon from "@lucide/svelte/icons/log-out";
  import MoreVerticalIcon from "@lucide/svelte/icons/more-vertical";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import SmartphoneIcon from "@lucide/svelte/icons/smartphone";
  import TabletIcon from "@lucide/svelte/icons/tablet";
  import GlobeIcon from "@lucide/svelte/icons/globe";
  import MapPinIcon from "@lucide/svelte/icons/map-pin";
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
      errorMessage = translateMessage(result.messages?.[0], undefined, m.common_errors_unknown());
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
      return D2Result.fail({ statusCode: 400, messages: ["account_sessions_no_session_selected"] });
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

  function deviceIcon(deviceType: string) {
    if (deviceType === "mobile") return SmartphoneIcon;
    if (deviceType === "tablet") return TabletIcon;
    if (deviceType === "desktop" || deviceType === "unknown") return MonitorIcon;
    return GlobeIcon;
  }

  function shortIp(ip: string | undefined): string {
    if (!ip) return m.account_sessions_unknown_ip();
    if (ip.length <= 24) return ip;
    return `${ip.slice(0, 12)}…${ip.slice(-8)}`;
  }

  function whoIsStub(id: string | undefined): string | undefined {
    if (!id || id.length < 8) return undefined;
    return `${id.slice(0, 4)}…${id.slice(-4)}`;
  }

  const otherSessionsCount = $derived(sessions.filter((s) => !s.isCurrent).length);
</script>

<section>
  <div>
    <h2 class="text-base font-semibold">{m.account_sessions_title()}</h2>
    <p class="text-muted-foreground mt-0.5 text-sm">{m.account_sessions_description()}</p>
  </div>
  <div class="mt-5 space-y-0">
    {#if !loaded}
      <ul class="divide-border/60 divide-y">
        {#each [0, 1] as _i (_i)}
          <li class="flex items-start gap-4 py-4">
            <Skeleton class="size-10 rounded-md" />
            <div class="flex flex-1 flex-col gap-2">
              <Skeleton class="h-5 w-44" />
              <Skeleton class="h-4 w-60" />
            </div>
            <Skeleton class="size-9 rounded-md" />
          </li>
        {/each}
      </ul>
      <div class="pt-4">
        <Skeleton class="h-9 w-56 rounded-md" />
      </div>
    {:else if errorMessage}
      <p class="text-destructive text-sm">{errorMessage}</p>
    {:else if sessions.length === 0}
      <p class="text-muted-foreground text-sm">{m.account_sessions_empty()}</p>
    {:else}
      <ul class="divide-border/60 divide-y border-y">
        {#each sessions as s (s.session.id)}
          {@const ua = parseUserAgent(s.session.userAgent)}
          {@const loc = formatLocation(s.whoIs)}
          {@const cc = locationCountryCode(s.whoIs)}
          {@const Icon = deviceIcon(ua.deviceType)}
          {@const whoIsStubText = whoIsStub(s.session.whoIsId)}
          <li
            class={[
              "group relative py-4 pl-4 pr-2 transition-colors hover:bg-muted/30",
              s.isCurrent &&
                "before:bg-success before:absolute before:left-0 before:top-3 before:bottom-3 before:w-[3px] before:rounded-full",
            ]
              .filter(Boolean)
              .join(" ")}
          >
            <div class="flex items-start gap-4">
              {#if s.session.clientFingerprint}
                <DeviceIdenticon
                  seed={s.session.clientFingerprint}
                  size={40}
                  class="shrink-0"
                />
              {:else}
                <div
                  class={[
                    "flex size-10 shrink-0 items-center justify-center rounded-md",
                    s.isCurrent ? "bg-success/10 text-success" : "bg-muted text-muted-foreground",
                  ].join(" ")}
                >
                  <Icon class="size-5" />
                </div>
              {/if}

              <div class="flex min-w-0 flex-1 flex-col gap-1.5">
                <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                  {#if s.isCurrent}
                    <Badge variant="success">{m.account_sessions_current_badge()}</Badge>
                  {/if}
                  <span class="text-muted-foreground text-xs"
                    >{fmtRelative(s.session.updatedAt)}</span
                  >
                </div>

                <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
                  <Icon class="text-muted-foreground size-4 shrink-0" />
                  <span class="font-semibold">{ua.browser}</span>
                  <span class="text-muted-foreground text-xs"
                    >{m.account_sessions_on_os({ os: ua.os })}</span
                  >
                </div>

                {#if loc}
                  <div
                    class="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs"
                  >
                    <span class="inline-flex items-center gap-1">
                      <MapPinIcon class="size-3" />
                      <span>{m.account_sessions_nearby_label()}</span>
                      {loc}
                      {#if cc}
                        <CountryFlag code={cc} />
                      {/if}
                    </span>
                  </div>
                {/if}
              </div>

              <Popover.Root>
                <Popover.Trigger>
                  {#snippet child({ props })}
                    <Button
                      {...props}
                      variant="ghost"
                      size="icon"
                      class="size-9 shrink-0"
                      aria-label={m.account_sessions_details()}
                    >
                      <MoreVerticalIcon class="size-4" />
                    </Button>
                  {/snippet}
                </Popover.Trigger>
                <Popover.Content
                  align="end"
                  class="w-72 p-3"
                >
                  <div class="space-y-2">
                    <p class="text-muted-foreground text-xs font-medium uppercase tracking-wide">
                      {m.account_sessions_details()}
                    </p>
                    <div class="flex flex-wrap items-center gap-1.5">
                      {#if s.session.ipAddress}
                        <CopyChip
                          label={m.account_sessions_ip_label()}
                          display={shortIp(s.session.ipAddress)}
                          value={s.session.ipAddress}
                        />
                      {/if}
                      {#if whoIsStubText}
                        <CopyChip
                          label={m.account_sessions_who_is_id_label()}
                          display={whoIsStubText}
                          value={s.session.whoIsId}
                        />
                      {/if}
                    </div>
                  </div>
                  {#if !s.isCurrent}
                    <div class="border-border/50 mt-3 border-t pt-3">
                      <Button
                        variant="outline"
                        size="sm"
                        onclick={() => openRevokeDialog(s)}
                        class="w-full"
                      >
                        <LogOutIcon class="mr-1.5 size-4" />
                        {m.account_sessions_revoke()}
                      </Button>
                    </div>
                  {/if}
                </Popover.Content>
              </Popover.Root>
            </div>
          </li>
        {/each}
      </ul>

      {#if otherSessionsCount > 0}
        <div class="pt-4">
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
  </div>
</section>

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
