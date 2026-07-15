<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { page } from "$app/state";
  import { cn } from "$lib/shared/utils/utils.js";
  import type { Component } from "svelte";

  interface NavItem {
    href: string;
    label: string;
    icon: Component;
  }

  let {
    items,
    class: className,
  }: {
    items: NavItem[];
    class?: string;
  } = $props();

  const currentPath = $derived(page.url.pathname);
</script>

<!-- eslint-disable svelte/no-navigation-without-resolve -- generic nav component: callers pass pre-resolved paths -->

<!-- Desktop: vertical left rail -->
<nav
  class={cn("flex flex-col gap-0.5", "max-md:hidden", "sticky top-20 w-52 shrink-0", className)}
  data-slot="settings-nav-desktop"
>
  {#each items as item (item.href)}
    {@const active = currentPath === item.href || currentPath.startsWith(item.href + "/")}
    <a
      href={item.href}
      class={cn(
        "flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors",
        active
          ? "bg-muted text-foreground"
          : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
      )}
    >
      <item.icon class="size-4" />
      {item.label}
    </a>
  {/each}
</nav>

<!-- Mobile: horizontal scrollable tabs -->
<nav
  class={cn("md:hidden", "scrollbar-none flex gap-0.5 overflow-x-auto border-b pb-2", className)}
  data-slot="settings-nav-mobile"
>
  {#each items as item (item.href)}
    {@const active = currentPath === item.href || currentPath.startsWith(item.href + "/")}
    <a
      href={item.href}
      class={cn(
        "flex shrink-0 items-center gap-1.5 rounded-md px-3 py-1.5 text-sm font-medium whitespace-nowrap transition-colors",
        active
          ? "bg-muted text-foreground"
          : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
      )}
    >
      <item.icon class="size-3.5" />
      {item.label}
    </a>
  {/each}
</nav>
