<script lang="ts">
  import { fly } from "svelte/transition";
  import * as m from "$lib/paraglide/messages.js";
  import { Button } from "$lib/client/components/ui/button/index.js";

  let {
    visible = false,
    onSave,
    onDiscard,
  }: {
    visible: boolean;
    onSave: () => void;
    onDiscard: () => void;
  } = $props();
</script>

{#if visible}
  <div
    class="bg-background/95 supports-backdrop-filter:bg-background/80 fixed inset-x-0 bottom-0 z-50 shadow-[0_-1px_3px_0_rgb(0_0_0/0.08)] backdrop-blur-sm dark:shadow-[0_-2px_6px_0_rgb(0_0_0/0.6)]"
    transition:fly={{ y: 40, duration: 200 }}
    data-slot="unsaved-changes-bar"
  >
    <div class="mx-auto flex max-w-3xl items-center justify-between px-4 py-3">
      <p class="text-muted-foreground text-sm">{m.common_ui_unsaved_changes()}</p>
      <div class="flex gap-2">
        <Button
          variant="ghost"
          size="sm"
          onclick={onDiscard}>{m.common_ui_discard()}</Button
        >
        <Button
          size="sm"
          onclick={onSave}>{m.common_ui_save()}</Button
        >
      </div>
    </div>
  </div>
{/if}
