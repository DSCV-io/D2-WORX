<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as m from "$lib/paraglide/messages.js";
  import PanelLeftIcon from "@lucide/svelte/icons/panel-left";
  import type { ComponentProps } from "svelte";
  import { useSidebar } from "./context.svelte.js";

  let {
    ref = $bindable(null),
    class: className,
    onclick,
    ...restProps
  }: ComponentProps<typeof Button> & {
    onclick?: (e: MouseEvent) => void;
  } = $props();

  const sidebar = useSidebar();
</script>

<Button
  data-sidebar="trigger"
  data-slot="sidebar-trigger"
  variant="ghost"
  size="icon"
  class={cn("size-7", className)}
  type="button"
  onclick={(e) => {
    onclick?.(e);
    sidebar.toggle();
  }}
  {...restProps}
>
  <PanelLeftIcon />
  <span class="sr-only">{m.common_ui_toggle_sidebar()}</span>
</Button>
