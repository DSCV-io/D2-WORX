<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import * as Tooltip from "$lib/client/components/ui/tooltip/index.js";
  import {
    listMySessions,
    revokeSession,
    revokeOtherSessions,
    type ActiveSessionDTO,
  } from "$lib/client/rest/account-client.js";
  import { D2Result } from "@d2/result";
  import { parseUserAgent } from "$lib/shared/utils/user-agent.js";
  import {
    formatLocation,
    formatLocationLong,
    locationCountryCode,
  } from "$lib/shared/utils/format-location.js";
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

  /**
   * Absolute timestamp for the row's time-chip tooltip — full date + time
   * with the user's saved timezone. Pairs with the relative label
   * ("13 minutes ago") shown in the chip itself.
   */
  function fmtAbsoluteDateTime(iso: string): string {
    return new Date(iso).toLocaleString(undefined, {
      dateStyle: "long",
      timeStyle: "long",
    });
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
          <li class="flex items-center gap-3 py-3 pl-4 pr-2">
            <Skeleton class="size-6 rounded-md" />
            <Skeleton class="h-4 flex-1" />
            <Skeleton class="size-6 rounded-md" />
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
      <ul
        class="divide-border/60 divide-y border-y md:grid md:grid-cols-[auto_auto_auto_auto_1fr_auto_auto] md:gap-x-4"
      >
        {#each sessions as s (s.session.id)}
          {@const ua = parseUserAgent(s.session.userAgent)}
          {@const loc = formatLocation(s.whoIs)}
          {@const cc = locationCountryCode(s.whoIs)}
          {@const Icon = deviceIcon(ua.deviceType)}
          {@const whoIsStubText = whoIsStub(s.session.whoIsId)}
          <li
            class={[
              "group text-muted-foreground relative flex flex-col gap-1.5 py-3 pl-4 pr-2 text-xs transition-colors hover:bg-muted/30 md:col-span-full md:grid md:grid-cols-subgrid md:grid-flow-dense md:items-center md:gap-x-4 md:gap-y-0",
              s.isCurrent &&
                "before:bg-info before:absolute before:left-0 before:top-3 before:bottom-3 before:w-[3px] before:rounded-full",
            ]
              .filter(Boolean)
              .join(" ")}
          >
            <div class="flex items-center gap-3 md:contents">
              {#if s.session.clientFingerprint}
                <DeviceIdenticon
                  seed={s.session.clientFingerprint}
                  size={24}
                  class="shrink-0"
                />
              {:else}
                <div
                  class={[
                    "flex size-6 shrink-0 items-center justify-center rounded-md",
                    s.isCurrent ? "bg-info/10 text-info" : "bg-muted text-muted-foreground",
                  ].join(" ")}
                >
                  <Icon class="size-3" />
                </div>
              {/if}

              <div class="flex items-center md:col-start-6 md:justify-self-end">
                {#if s.isCurrent}
                  <Badge
                    variant="info"
                    class="px-1.5 py-0 text-[10px]"
                  >
                    {m.account_sessions_current_badge()}
                  </Badge>
                {/if}
              </div>

              <div class="ml-auto md:col-start-7 md:ml-0">
                <Popover.Root>
                <Popover.Trigger>
                  {#snippet child({ props })}
                    <Button
                      {...props}
                      variant="ghost"
                      size="icon"
                      class="size-6 shrink-0"
                      aria-label={m.account_sessions_details()}
                    >
                      <MoreVerticalIcon class="size-3" />
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
            </div>

            <Tooltip.Provider delayDuration={150}>
              <Tooltip.Root>
                <Tooltip.Trigger>
                  {#snippet child({ props })}
                    <span
                      {...props}
                      class="ml-9 inline-flex items-center gap-1 whitespace-nowrap md:ml-0 md:col-start-2"
                    >
                      <ClockIcon class="size-3" />
                      {fmtRelative(s.session.updatedAt)}
                    </span>
                  {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>
                  <p class="text-xs">{fmtAbsoluteDateTime(s.session.updatedAt)}</p>
                </Tooltip.Content>
              </Tooltip.Root>
            </Tooltip.Provider>

            {#if loc}
              {@const locLong = formatLocationLong(s.whoIs)}
              <Tooltip.Provider delayDuration={150}>
                <Tooltip.Root>
                  <Tooltip.Trigger>
                    {#snippet child({ props })}
                      <span
                        {...props}
                        class="ml-9 inline-flex items-center gap-1 whitespace-nowrap md:ml-0 md:col-start-3"
                      >
                        <MapPinIcon class="size-3" />
                        {loc}
                        {#if cc}
                          <CountryFlag code={cc} />
                        {/if}
                      </span>
                    {/snippet}
                  </Tooltip.Trigger>
                  <Tooltip.Content>
                    <p class="text-xs">{locLong}</p>
                  </Tooltip.Content>
                </Tooltip.Root>
              </Tooltip.Provider>
            {:else}
              <span class="hidden md:block md:col-start-3"></span>
            {/if}

            <Tooltip.Provider delayDuration={150}>
              <Tooltip.Root>
                <Tooltip.Trigger>
                  {#snippet child({ props })}
                    <span
                      {...props}
                      class="ml-9 inline-flex items-center gap-1 whitespace-nowrap md:ml-0 md:col-start-4"
                    >
                      <Icon class="size-3" />
                      <span class="text-foreground font-medium">{ua.browser}</span>
                      <span>{m.account_sessions_on_os({ os: ua.os })}</span>
                    </span>
                  {/snippet}
                </Tooltip.Trigger>
                <Tooltip.Content>
                  <p class="text-xs">
                    {ua.browserLong} {m.account_sessions_on_os({ os: ua.osLong })}
                  </p>
                </Tooltip.Content>
              </Tooltip.Root>
            </Tooltip.Provider>
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
