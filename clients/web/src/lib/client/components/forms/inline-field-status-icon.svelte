<script lang="ts">
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import CircleXIcon from "@lucide/svelte/icons/circle-x";
  import InfoIcon from "@lucide/svelte/icons/info";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  type SaveState = "idle" | "saving" | "saved" | "error";
  type ValidationStatus = "idle" | "validating" | "valid" | "invalid";

  let {
    saveState,
    validationStatus,
    dirty,
  }: {
    saveState: SaveState;
    validationStatus: ValidationStatus;
    dirty: boolean;
  } = $props();
</script>

{#if saveState === "saving"}
  <LoaderCircleIcon class="text-muted-foreground size-3.5 animate-spin" />
{:else if saveState === "saved"}
  <CircleCheckIcon class="size-3.5 text-green-500" />
{:else if validationStatus === "validating"}
  <LoaderCircleIcon class="text-muted-foreground size-3.5 animate-spin" />
{:else if validationStatus === "invalid"}
  <CircleXIcon class="text-destructive size-3.5" />
{:else if validationStatus === "valid" || dirty}
  <InfoIcon class="size-3.5 text-blue-500" />
{/if}
