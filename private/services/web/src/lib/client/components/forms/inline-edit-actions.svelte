<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import * as m from "$lib/paraglide/messages.js";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Undo2Icon from "@lucide/svelte/icons/undo-2";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  type SaveState = "idle" | "saving" | "saved" | "error";

  let {
    dirty,
    saveState,
    saveDisabled = false,
    onSave,
    onRevert,
  }: {
    dirty: boolean;
    saveState: SaveState;
    saveDisabled?: boolean;
    onSave: () => void;
    onRevert: () => void;
  } = $props();

  // Slot is "open" whenever there's something to show: dirty (save+revert),
  // saving (spinner), or saved (checkmark). Idle = collapsed = field gets the
  // full row width back.
  const isOpen = $derived(dirty || saveState === "saving" || saveState === "saved");

  const ACTION_BTN =
    "text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 cursor-pointer items-center justify-center rounded-md transition-colors";
</script>

<!--
  Container animates `width` (0 ↔ 4.625rem) on dirty/saving/saved transitions
  so the input on the left can claim the row when nothing's pending.

  `inert` on the collapsed wrapper keeps the buttons unfocusable when hidden
  — preserves correct tab order without juggling `tabindex` per state.

  `motion-reduce:transition-none` honours the user's OS reduced-motion setting.
-->
<div
  class={cn(
    "flex shrink-0 items-stretch overflow-hidden transition-[width] duration-150 ease-out motion-reduce:transition-none",
    isOpen ? "w-[4.625rem]" : "w-0",
  )}
  inert={!isOpen}
  aria-hidden={!isOpen}
>
  <div
    class={cn(
      "flex w-[4.625rem] shrink-0 justify-center gap-0.5 transition-[opacity,transform] duration-150 ease-out motion-reduce:transition-none",
      isOpen ? "translate-x-0 opacity-100" : "pointer-events-none translate-x-2 opacity-0",
    )}
  >
    {#if saveState === "saving"}
      <div class="flex size-9 items-center justify-center">
        <LoaderCircleIcon class="text-muted-foreground size-4 animate-spin" />
      </div>
    {:else if saveState === "saved"}
      <div class="flex size-9 items-center justify-center">
        <CircleCheckIcon class="size-4 text-green-500" />
      </div>
    {:else if dirty}
      <button
        type="button"
        onclick={onSave}
        disabled={saveDisabled}
        class={cn(ACTION_BTN, saveDisabled && "cursor-not-allowed opacity-50")}
      >
        <SaveIcon class="size-4" />
        <span class="sr-only">{m.common_ui_save()}</span>
      </button>
      <button
        type="button"
        onclick={onRevert}
        class={ACTION_BTN}
      >
        <Undo2Icon class="size-4" />
        <span class="sr-only">{m.common_ui_revert()}</span>
      </button>
    {/if}
  </div>
</div>
