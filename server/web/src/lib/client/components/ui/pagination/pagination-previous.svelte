<script lang="ts">
  import { Pagination as PaginationPrimitive } from "bits-ui";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import { buttonVariants } from "$lib/client/components/ui/button/index.js";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as m from "$lib/paraglide/messages.js";

  let {
    ref = $bindable(null),
    class: className,
    ...restProps
  }: PaginationPrimitive.PrevButtonProps = $props();
</script>

<PaginationPrimitive.PrevButton
  bind:ref
  aria-label={m.common_ui_pagination_previous()}
  class={cn(
    buttonVariants({
      size: "default",
      variant: "ghost",
      class: "gap-1 px-2.5 sm:ps-2.5",
    }),
    className,
  )}
  {...restProps}
>
  <ChevronLeftIcon />
  <span class="hidden sm:block">Previous</span></PaginationPrimitive.PrevButton
>
