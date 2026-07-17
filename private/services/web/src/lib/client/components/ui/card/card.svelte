<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import type { HTMLAttributes } from "svelte/elements";
  import { cn, type WithElementRef } from "$lib/shared/utils/utils.js";

  let {
    ref = $bindable(null),
    class: className,
    children,
    ...restProps
  }: WithElementRef<HTMLAttributes<HTMLDivElement>> = $props();
</script>

<div
  bind:this={ref}
  data-slot="card"
  class={cn(
    "bg-card text-card-foreground flex flex-col gap-6 rounded-xl py-6 shadow-sm dark:shadow-[0_1px_3px_0_rgb(0_0_0/0.5)]",
    className,
  )}
  {...restProps}
>
  {@render children?.()}
</div>
