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
  }: {
    open?: boolean;
    title: string;
    description: string;
    confirmLabel?: string;
    cancelLabel?: string;
    variant?: "default" | "destructive";
    onConfirm: () => Promise<void>;
  } = $props();

  let loading = $state(false);

  async function handleConfirm() {
    loading = true;
    try {
      await onConfirm();
      open = false;
    } finally {
      loading = false;
    }
  }
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
