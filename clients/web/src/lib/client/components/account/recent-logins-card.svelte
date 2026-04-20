<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import { listRecentLogins, type RecentLoginDTO } from "$lib/client/rest/account-client.js";
  import { parseUserAgent } from "$lib/shared/utils/user-agent.js";
  import { formatLocation } from "$lib/shared/utils/format-location.js";
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

  async function load(newOffset: number) {
    loading = true;
    errorMessage = "";
    const result = await listRecentLogins(PAGE_SIZE, newOffset);
    loading = false;
    loaded = true;
    if (!result.success) {
      errorMessage = result.messages?.[0] ?? m.common_errors_unknown();
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
    return new Date(iso).toLocaleString();
  }

  function shortIp(ip: string): string {
    if (!ip) return "—";
    if (ip.length <= 24) return ip;
    return `${ip.slice(0, 12)}…${ip.slice(-8)}`;
  }

  /** First/last 4 chars of the content-addressable WhoIs hash — quick visual diff between rows. */
  function whoIsStub(id: string | undefined): string | undefined {
    if (!id || id.length < 8) return undefined;
    return `${id.slice(0, 4)}…${id.slice(-4)}`;
  }

  /** Pick a Lucide icon for the device class returned by ua-parser. */
  function deviceIcon(deviceType: string) {
    if (deviceType === "mobile") return SmartphoneIcon;
    if (deviceType === "tablet") return TabletIcon;
    if (deviceType === "desktop" || deviceType === "unknown") return MonitorIcon;
    return GlobeIcon;
  }
</script>

<Card.Root>
  <Card.Header>
    <Card.Title class="text-base">{m.account_recent_logins_title()}</Card.Title>
    <Card.Description>{m.account_recent_logins_description()}</Card.Description>
  </Card.Header>
  <Card.Content class="space-y-4">
    {#if !loaded}
      <ul class="space-y-3">
        {#each Array.from({ length: PAGE_SIZE }) as _ , i (i)}
          <li class="flex items-start gap-4 rounded-lg border p-4">
            <Skeleton class="size-10 rounded-md" />
            <div class="flex flex-1 flex-col gap-2">
              <Skeleton class="h-5 w-48" />
              <Skeleton class="h-4 w-40" />
              <Skeleton class="h-4 w-60" />
            </div>
          </li>
        {/each}
      </ul>
      <div class="flex items-center justify-between gap-2 border-t pt-4">
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
      <ul class="space-y-3">
        {#each events as e (e.event.id)}
          {@const ua = parseUserAgent(e.event.userAgent)}
          {@const loc = formatLocation(e.whoIs)}
          {@const ok = e.event.successful}
          {@const stub = whoIsStub(e.event.whoIsId)}
          {@const Icon = deviceIcon(ua.deviceType)}
          <li
            class={[
              "flex items-start gap-4 rounded-lg border p-4 transition-colors shadow-sm",
              ok
                ? "hover:border-muted-foreground/30"
                : "border-destructive/30 bg-destructive/5",
            ].join(" ")}
          >
            <div
              class={[
                "flex size-10 shrink-0 items-center justify-center rounded-md",
                ok ? "bg-muted text-muted-foreground" : "bg-destructive/10 text-destructive",
              ].join(" ")}
            >
              <Icon class="size-5" />
            </div>

            <div class="flex flex-1 flex-col gap-1.5 min-w-0">
              <div class="flex flex-wrap items-center gap-x-2 gap-y-1">
                <Badge variant={ok ? "success" : "destructive"}>
                  {ok ? m.account_recent_logins_success() : m.account_recent_logins_failure()}
                </Badge>
                <span class="text-muted-foreground text-xs">{fmtDateTime(e.event.createdAt)}</span>
              </div>

              <div class="flex flex-wrap items-center gap-x-2 gap-y-1 text-sm">
                <span>
                  <span class="font-medium">{ua.browser}</span>
                  <span class="text-muted-foreground"
                    >&nbsp;{m.account_sessions_on_os({ os: ua.os })}</span
                  >
                </span>
                {#if stub}
                  <span
                    class="text-muted-foreground/70 font-mono text-[10px]"
                    title={e.event.whoIsId}
                  >
                    ({stub})
                  </span>
                {/if}
              </div>

              <div class="text-muted-foreground flex flex-wrap items-center gap-x-3 gap-y-1 text-xs">
                {#if loc}
                  <span class="inline-flex items-center gap-1">
                    <MapPinIcon class="size-3" />
                    {loc}
                  </span>
                {/if}
                <span class="font-mono" title={e.event.ipAddress}>
                  {shortIp(e.event.ipAddress)}
                </span>
              </div>
            </div>
          </li>
        {/each}
      </ul>

      <div class="text-muted-foreground flex items-center justify-between gap-2 border-t pt-4 text-xs">
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
  </Card.Content>
</Card.Root>
