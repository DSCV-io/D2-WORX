<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import { page } from "$app/stores";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import { listRecentLogins, type RecentLoginDTO } from "$lib/client/rest/account-client.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { parseUserAgent } from "$lib/shared/utils/user-agent.js";
  import { formatLocation, locationCountryCode } from "$lib/shared/utils/format-location.js";
  import CopyChip from "./copy-chip.svelte";
  import CountryFlag from "./country-flag.svelte";
  import DeviceIdenticon from "./device-identicon.svelte";
  import MoreVerticalIcon from "@lucide/svelte/icons/more-vertical";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import SmartphoneIcon from "@lucide/svelte/icons/smartphone";
  import TabletIcon from "@lucide/svelte/icons/tablet";
  import GlobeIcon from "@lucide/svelte/icons/globe";
  import MapPinIcon from "@lucide/svelte/icons/map-pin";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";

  const PAGE_SIZE = 5;

  let events = $state<RecentLoginDTO[]>([]);
  let total = $state(0);
  let offset = $state(0);
  let loaded = $state(false);
  let loading = $state(false);
  let errorMessage = $state("");

  // User's preferred timezone (synced from cookie via root layout). Falls back
  // to browser tz so SSR / pre-cookie loads still render readable times.
  const timezone = $derived(
    ($page.data as { timezone?: string }).timezone ??
      Intl.DateTimeFormat().resolvedOptions().timeZone,
  );

  async function load(newOffset: number) {
    loading = true;
    errorMessage = "";
    const result = await listRecentLogins(PAGE_SIZE, newOffset);
    loading = false;
    loaded = true;
    if (!result.success) {
      errorMessage = translateMessage(result.messages?.[0], undefined, m.common_errors_unknown());
      events = [];
      total = 0;
      return;
    }
    events = result.data?.events ?? [];
    total = result.data?.total ?? 0;
    offset = newOffset;
  }

  $effect(() => {
    void load(0);
  });

  const hasPrev = $derived(offset > 0);
  const hasNext = $derived(offset + PAGE_SIZE < total);
  const pageStart = $derived(total === 0 ? 0 : offset + 1);
  const pageEnd = $derived(Math.min(offset + PAGE_SIZE, total));

  function fmtDateTime(iso: string): string {
    return new Date(iso).toLocaleString(undefined, {
      timeZone: timezone,
      dateStyle: "medium",
      timeStyle: "short",
    });
  }

  function shortIp(ip: string): string {
    if (!ip) return "—";
    if (ip.length <= 24) return ip;
    return `${ip.slice(0, 12)}…${ip.slice(-8)}`;
  }

  function whoIsStub(id: string | undefined): string | undefined {
    if (!id || id.length < 8) return undefined;
    return `${id.slice(0, 4)}…${id.slice(-4)}`;
  }

  function deviceFpStub(fp: string | undefined): string | undefined {
    if (!fp || fp.length < 8) return undefined;
    return `${fp.slice(0, 4)}…${fp.slice(-4)}`;
  }

  function deviceIcon(deviceType: string) {
    if (deviceType === "mobile") return SmartphoneIcon;
    if (deviceType === "tablet") return TabletIcon;
    if (deviceType === "desktop" || deviceType === "unknown") return MonitorIcon;
    return GlobeIcon;
  }
</script>

<section>
  <div>
    <h2 class="text-base font-semibold">{m.account_recent_logins_title()}</h2>
    <p class="text-muted-foreground mt-0.5 text-sm">{m.account_recent_logins_description()}</p>
  </div>
  <div class="mt-5 space-y-0">
    {#if !loaded}
      <ul class="divide-border/60 divide-y border-y">
        {#each Array.from({ length: PAGE_SIZE }) as _, i (i)}
          <li class="flex items-start gap-4 py-4">
            <Skeleton class="size-10 rounded-md" />
            <div class="flex flex-1 flex-col gap-2">
              <Skeleton class="h-5 w-48" />
              <Skeleton class="h-4 w-40" />
              <Skeleton class="h-4 w-60" />
            </div>
            <Skeleton class="size-9 rounded-md" />
          </li>
        {/each}
      </ul>
      <div class="flex items-center justify-between gap-2 pt-4">
        <Skeleton class="h-4 w-32" />
        <div class="flex gap-1">
          <Skeleton class="size-9 rounded-md" />
          <Skeleton class="size-9 rounded-md" />
        </div>
      </div>
    {:else if errorMessage}
      <p class="text-destructive text-sm">{errorMessage}</p>
    {:else if events.length === 0}
      <p class="text-muted-foreground text-sm">{m.account_recent_logins_empty()}</p>
    {:else}
      <ul class="divide-border/60 divide-y border-y">
        {#each events as e (e.event.id)}
          {@const ua = parseUserAgent(e.event.userAgent)}
          {@const loc = formatLocation(e.whoIs)}
          {@const cc = locationCountryCode(e.whoIs)}
          {@const ok = e.event.successful}
          {@const whoIsStubText = whoIsStub(e.event.whoIsId)}
          {@const deviceFpStubText = deviceFpStub(e.event.deviceFingerprint)}
          {@const Icon = deviceIcon(ua.deviceType)}
          {@const hasForensic = !!(e.event.ipAddress || whoIsStubText || deviceFpStubText)}
          <li
            class={[
              "relative flex items-start gap-4 py-4 pl-4 pr-2 transition-colors hover:bg-muted/30",
              !ok &&
                "before:bg-destructive before:absolute before:left-0 before:top-3 before:bottom-3 before:w-[3px] before:rounded-full",
            ]
              .filter(Boolean)
              .join(" ")}
          >
            {#if e.event.clientFingerprint}
              <DeviceIdenticon
                seed={e.event.clientFingerprint}
                size={40}
                class="shrink-0"
              />
            {:else}
              <div
                class={[
                  "flex size-10 shrink-0 items-center justify-center rounded-md",
                  ok ? "bg-muted text-muted-foreground" : "bg-destructive/10 text-destructive",
                ].join(" ")}
              >
                <Icon class="size-5" />
              </div>
            {/if}

            <div class="flex min-w-0 flex-1 flex-col gap-1.5">
              <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                <Badge variant={ok ? "success" : "destructive"}>
                  {ok ? m.account_recent_logins_success() : m.account_recent_logins_failure()}
                </Badge>
                <span class="text-muted-foreground text-xs">{fmtDateTime(e.event.createdAt)}</span>
              </div>

              <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
                <Icon class="text-muted-foreground size-4 shrink-0" />
                <span>
                  <span class="font-medium">{ua.browser}</span>
                  <span class="text-muted-foreground"
                    >&nbsp;{m.account_sessions_on_os({ os: ua.os })}</span
                  >
                </span>
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

            {#if hasForensic}
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
                      {#if e.event.ipAddress}
                        <CopyChip
                          label={m.account_sessions_ip_label()}
                          display={shortIp(e.event.ipAddress)}
                          value={e.event.ipAddress}
                        />
                      {/if}
                      {#if whoIsStubText}
                        <CopyChip
                          label={m.account_sessions_who_is_id_label()}
                          display={whoIsStubText}
                          value={e.event.whoIsId}
                        />
                      {/if}
                      {#if deviceFpStubText}
                        <CopyChip
                          label={m.account_sessions_device_fp_label()}
                          display={deviceFpStubText}
                          value={e.event.deviceFingerprint}
                        />
                      {/if}
                    </div>
                  </div>
                </Popover.Content>
              </Popover.Root>
            {/if}
          </li>
        {/each}
      </ul>

      <div class="text-muted-foreground flex items-center justify-between gap-2 pt-4 text-xs">
        <span>{m.account_recent_logins_pagination({ start: pageStart, end: pageEnd, total })}</span>
        <div class="flex gap-1">
          <Button
            variant="outline"
            size="sm"
            disabled={!hasPrev || loading}
            onclick={() => void load(Math.max(0, offset - PAGE_SIZE))}
            aria-label={m.common_ui_previous()}
          >
            <ChevronLeftIcon class="size-4" />
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!hasNext || loading}
            onclick={() => void load(offset + PAGE_SIZE)}
            aria-label={m.common_ui_next()}
          >
            <ChevronRightIcon class="size-4" />
          </Button>
        </div>
      </div>
    {/if}
  </div>
</section>
