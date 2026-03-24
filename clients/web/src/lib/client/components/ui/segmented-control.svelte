<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import type { Component } from "svelte";
  import { Tween } from "svelte/motion";
  import { cubicOut } from "svelte/easing";

  interface Segment {
    value: string;
    label: string;
    icon?: Component;
  }

  let {
    segments,
    value = $bindable(""),
    size = "default",
    onchange,
    class: className,
  }: {
    segments: Segment[];
    value?: string;
    size?: "sm" | "default";
    onchange?: (value: string) => void;
    class?: string;
  } = $props();

  const activeIndex = $derived(
    Math.max(
      0,
      segments.findIndex((s) => s.value === value),
    ),
  );

  // Tweened position — handles animation regardless of how state updates
  const pillPosition = new Tween(0, { duration: 200, easing: cubicOut });

  // Sync activeIndex to the tween whenever it changes
  $effect(() => {
    pillPosition.set(activeIndex);
  });

  const pad = $derived(size === "sm" ? 3 : 4);

  const sizeClasses = $derived(
    size === "sm" ? { icon: "size-3", text: "text-xs" } : { icon: "size-3.5", text: "text-sm" },
  );
</script>

<div
  class={cn("bg-muted relative grid items-stretch rounded-lg", className)}
  style="grid-template-columns: repeat({segments.length}, 1fr); padding: {pad}px"
  data-slot="segmented-control"
>
  <!-- Pill: spans one grid cell, position driven by tweened value -->
  <div
    class="bg-background absolute rounded-md shadow-sm"
    style="
      top: {pad}px;
      bottom: {pad}px;
      left: {pad}px;
      width: calc((100% - {pad * 2}px) / {segments.length});
      transform: translateX({pillPosition.current * 100}%);
    "
  ></div>

  {#each segments as segment (segment.value)}
    {@const active = segment.value === value}
    <button
      type="button"
      onclick={() => {
        value = segment.value;
        onchange?.(segment.value);
      }}
      class={cn(
        "relative z-10 flex cursor-pointer items-center justify-center gap-1.5 rounded-md px-4 py-1.5 font-medium whitespace-nowrap transition-colors",
        sizeClasses.text,
        active ? "text-foreground" : "text-muted-foreground hover:text-foreground/80",
      )}
    >
      {#if segment.icon}
        <segment.icon class={sizeClasses.icon} />
      {/if}
      {segment.label}
    </button>
  {/each}
</div>
