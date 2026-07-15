<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as AlertDialog from "$lib/client/components/ui/alert-dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  let {
    open = $bindable(false),
    title,
    description,
    confirmLabel,
    cancelLabel,
    variant = "default",
    onConfirm,
    onCancel,
  }: {
    open?: boolean;
    title: string;
    description: string;
    confirmLabel?: string;
    cancelLabel?: string;
    variant?: "default" | "destructive";
    onConfirm: () => Promise<void>;
    /**
     * Fires when the dialog is dismissed without confirming — explicit Cancel
     * button click, Escape key, or click outside. Use it to undo any optimistic
     * state changes the caller made before opening the dialog.
     */
    onCancel?: () => void;
  } = $props();

  let loading = $state(false);
  // Tracks whether the most recent open→close transition went through confirm.
  // Used by the $effect below to fire onCancel for any other close path.
  let confirmedThisOpen = $state(false);
  let wasOpen = $state(false);

  async function handleConfirm() {
    loading = true;
    confirmedThisOpen = true;
    try {
      await onConfirm();
      open = false;
    } finally {
      loading = false;
    }
  }

  $effect(() => {
    if (open) {
      wasOpen = true;
      confirmedThisOpen = false;
    } else if (wasOpen) {
      // Dialog just closed. If onConfirm wasn't the trigger, treat as cancel.
      if (!confirmedThisOpen) onCancel?.();
      wasOpen = false;
    }
  });
</script>

<AlertDialog.Root bind:open>
  <AlertDialog.Content>
    <AlertDialog.Header>
      <AlertDialog.Title>{title}</AlertDialog.Title>
      <AlertDialog.Description>{description}</AlertDialog.Description>
    </AlertDialog.Header>
    <AlertDialog.Footer>
      <AlertDialog.Cancel disabled={loading}
        >{cancelLabel ?? m.common_ui_cancel()}</AlertDialog.Cancel
      >
      <Button
        variant={variant === "destructive" ? "destructive" : "default"}
        onclick={handleConfirm}
        disabled={loading}
      >
        {#if loading}
          <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
        {/if}
        {confirmLabel ?? m.common_ui_confirm()}
      </Button>
    </AlertDialog.Footer>
  </AlertDialog.Content>
</AlertDialog.Root>
