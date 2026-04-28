<script lang="ts">
  import { dev } from "$app/environment";
  import { invalidateAll } from "$app/navigation";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import ActivityIcon from "@lucide/svelte/icons/activity";
  import RefreshCwIcon from "@lucide/svelte/icons/refresh-cw";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import CircleXIcon from "@lucide/svelte/icons/circle-x";
  import * as m from "$lib/paraglide/messages.js";
  import TriangleAlertIcon from "@lucide/svelte/icons/triangle-alert";

  let { data } = $props();
  let refreshing = $state(false);

  async function refresh() {
    refreshing = true;
    await invalidateAll();
    refreshing = false;
  }

  function statusColor(status: string): string {
    if (status === "healthy") return "text-green-600 dark:text-green-400";
    if (status === "degraded") return "text-yellow-600 dark:text-yellow-400";
    return "text-red-600 dark:text-red-400";
  }

  function statusBg(status: string): string {
    if (status === "healthy") return "bg-green-100 dark:bg-green-950";
    if (status === "degraded") return "bg-yellow-100 dark:bg-yellow-950";
    return "bg-red-100 dark:bg-red-950";
  }

  // Map raw API status strings to localized labels. Falls back to the raw
  // status string for unknown values (defensive — surfaces unexpected new
  // statuses the API might add without breaking the UI).
  function statusLabel(status: string): string {
    if (status === "healthy") return m.webclient_debug_health_status_healthy();
    if (status === "degraded") return m.webclient_debug_health_status_degraded();
    if (status === "unhealthy") return m.webclient_debug_health_status_unhealthy();
    return status;
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_health_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_debug_health_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
  <meta
    property="og:title"
    content="{m.webclient_debug_health_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_debug_health_description()}
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
          <ActivityIcon class="text-muted-foreground size-5" />
        </div>
        <div>
          <h1 class="text-2xl font-bold">{m.webclient_debug_health_title()}</h1>
          <p class="text-muted-foreground text-sm">
            {m.webclient_debug_health_dev_description()}
          </p>
        </div>
      </div>
      <Button
        variant="outline"
        size="sm"
        onclick={refresh}
        disabled={refreshing}
      >
        <RefreshCwIcon class="mr-2 size-4" />
        {refreshing ? m.webclient_debug_health_refreshing() : m.common_ui_refresh()}
      </Button>
    </div>

    {#if data.error}
      <Card.Root>
        <Card.Header>
          <Card.Title class="flex items-center gap-2 text-base text-red-600 dark:text-red-400">
            <CircleXIcon class="size-5" />
            {m.webclient_debug_health_gateway_unreachable()}
          </Card.Title>
          <Card.Description>
            {m.webclient_debug_health_gateway_unreachable_description()}
          </Card.Description>
        </Card.Header>
        <Card.Content>
          <pre class="bg-muted rounded p-3 font-mono text-xs">{data.error}</pre>
        </Card.Content>
      </Card.Root>
    {:else if data.health}
      {@const health = data.health}

      <!-- Overall Status Banner -->
      <div class="flex items-center gap-3 rounded-lg border p-4 {statusBg(health.status)}">
        {#if health.status === "healthy"}
          <CircleCheckIcon class="size-6 {statusColor(health.status)}" />
        {:else if health.status === "degraded"}
          <TriangleAlertIcon class="size-6 {statusColor(health.status)}" />
        {:else}
          <CircleXIcon class="size-6 {statusColor(health.status)}" />
        {/if}
        <div>
          <p class="font-semibold {statusColor(health.status)}">
            {statusLabel(health.status)}
          </p>
          <p class="text-muted-foreground text-xs">
            {new Date(health.timestamp).toLocaleString()}
          </p>
        </div>
      </div>

      <!-- Service Cards -->
      <div class="grid gap-4 md:grid-cols-2">
        {#each Object.entries(health.services) as [serviceName, service] (serviceName)}
          <Card.Root>
            <Card.Header class="pb-3">
              <Card.Title class="flex items-center justify-between text-base">
                <span class="capitalize">{serviceName}</span>
                <span
                  class="flex items-center gap-1.5 text-sm font-normal {statusColor(
                    service.status,
                  )}"
                >
                  {#if service.status === "healthy"}
                    <CircleCheckIcon class="size-4" />
                  {:else if service.status === "degraded"}
                    <TriangleAlertIcon class="size-4" />
                  {:else}
                    <CircleXIcon class="size-4" />
                  {/if}
                  {statusLabel(service.status)}
                </span>
              </Card.Title>
              <Card.Description>
                {m.webclient_debug_health_round_trip({ ms: service.latencyMs })}
              </Card.Description>
            </Card.Header>
            <Card.Content>
              {#if Object.keys(service.components).length > 0}
                <dl class="space-y-2 text-sm">
                  {#each Object.entries(service.components) as [compName, comp], i (compName)}
                    {#if i > 0}
                      <Separator />
                    {/if}
                    <div class="flex items-center justify-between">
                      <dt class="text-muted-foreground">{compName}</dt>
                      <dd class="flex items-center gap-2">
                        <span class="text-muted-foreground text-xs">{comp.latencyMs}ms</span>
                        <span class="text-xs font-medium {statusColor(comp.status)}">
                          {statusLabel(comp.status)}
                        </span>
                      </dd>
                    </div>
                    {#if comp.error}
                      <div
                        class="rounded bg-red-50 p-2 font-mono text-xs text-red-700 dark:bg-red-950 dark:text-red-300"
                      >
                        {comp.error}
                      </div>
                    {/if}
                  {/each}
                </dl>
              {:else}
                <p class="text-muted-foreground text-sm italic">
                  {m.webclient_debug_health_no_component_details()}
                </p>
              {/if}
            </Card.Content>
          </Card.Root>
        {/each}
      </div>

      <!-- Raw JSON -->
      <Card.Root>
        <Card.Header>
          <Card.Title class="text-base">{m.webclient_debug_health_raw_response()}</Card.Title>
          <Card.Description>
            {m.webclient_debug_health_source_gateway({ path: "GET /api/health" })}
          </Card.Description>
        </Card.Header>
        <Card.Content>
          <pre
            class="bg-muted max-h-96 overflow-auto rounded p-3 font-mono text-xs">{JSON.stringify(
              health,
              null,
              2,
            )}</pre>
        </Card.Content>
      </Card.Root>
    {/if}
  </div>
{/if}
