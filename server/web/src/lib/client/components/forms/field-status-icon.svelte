<script lang="ts">
  import type { FieldStatus } from "$lib/shared/forms/field-status.js";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import CircleXIcon from "@lucide/svelte/icons/circle-x";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  type Props = {
    status: FieldStatus;
  };

  let { status }: Props = $props();
</script>

{#if status === "valid"}
  <CircleCheckIcon class="text-success size-3.5" />
{:else if status === "invalid"}
  <CircleXIcon class="text-destructive size-3.5" />
{:else if status === "validating"}
  <LoaderCircleIcon class="text-muted-foreground size-3.5 animate-spin" />
{/if}
