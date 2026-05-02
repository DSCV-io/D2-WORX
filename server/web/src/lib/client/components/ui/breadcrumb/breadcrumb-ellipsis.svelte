<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import EllipsisIcon from "@lucide/svelte/icons/ellipsis";
  import type { HTMLAttributes } from "svelte/elements";
  import { cn, type WithElementRef, type WithoutChildren } from "$lib/shared/utils/utils.js";
  import * as m from "$lib/paraglide/messages.js";

  let {
    ref = $bindable(null),
    class: className,
    ...restProps
  }: WithoutChildren<WithElementRef<HTMLAttributes<HTMLSpanElement>>> = $props();
</script>

<span
  bind:this={ref}
  data-slot="breadcrumb-ellipsis"
  role="presentation"
  aria-hidden="true"
  class={cn("flex size-9 items-center justify-center", className)}
  {...restProps}
>
  <EllipsisIcon class="size-4" />
  <span class="sr-only">{m.common_ui_more()}</span>
</span>
