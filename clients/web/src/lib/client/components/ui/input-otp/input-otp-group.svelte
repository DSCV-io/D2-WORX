<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import type { Snippet } from "svelte";

  let {
    children,
    class: className,
    ...rest
  }: {
    children?: Snippet;
    class?: string;
  } & Record<string, unknown> = $props();
</script>

<div
  data-slot="input-otp-group"
  class={cn("flex items-center", className)}
  {...rest}
>
  {@render children?.()}
</div>
