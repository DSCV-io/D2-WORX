<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
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

  const ACTION_BTN =
    "text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 cursor-pointer items-center justify-center rounded-md transition-colors";
</script>

<div class="flex w-[4.625rem] shrink-0 justify-center gap-0.5">
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
      <span class="sr-only">Save</span>
    </button>
    <button
      type="button"
      onclick={onRevert}
      class={ACTION_BTN}
    >
      <Undo2Icon class="size-4" />
      <span class="sr-only">Revert</span>
    </button>
  {/if}
</div>
