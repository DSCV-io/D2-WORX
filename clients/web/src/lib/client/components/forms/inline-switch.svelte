<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import { Switch } from "$lib/client/components/ui/switch/index.js";
  import InlineEditActions from "./inline-edit-actions.svelte";

  type SaveState = "idle" | "saving" | "saved" | "error";

  let {
    value = $bindable(false),
    label,
    description,
    onSave,
    onDirtyChange,
    class: className,
  }: {
    value?: boolean;
    label: string;
    description?: string;
    onSave: (value: boolean) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  let originalValue = $state(value);
  let currentValue = $state(value);
  let saveState: SaveState = $state("idle");
  let errorMessage = $state("");

  $effect(() => {
    originalValue = value;
    currentValue = value;
  });

  const isDirty = $derived(currentValue !== originalValue);

  $effect(() => {
    onDirtyChange?.(isDirty);
  });

  export function revert() {
    currentValue = originalValue;
    errorMessage = "";
    saveState = "idle";
  }

  export function getDirty(): boolean {
    return isDirty;
  }

  export async function saveIfDirty(): Promise<boolean> {
    if (!isDirty) return true;
    return doSave();
  }

  async function doSave(): Promise<boolean> {
    saveState = "saving";
    errorMessage = "";
    try {
      await onSave(currentValue);
      value = currentValue;
      originalValue = currentValue;
      saveState = "saved";
      setTimeout(() => {
        if (saveState === "saved") saveState = "idle";
      }, 2000);
      return true;
    } catch (err) {
      errorMessage = err instanceof Error ? err.message : "Failed to save.";
      saveState = "error";
      return false;
    }
  }

  const fieldId = $derived(label.toLowerCase().replace(/\s+/g, "-"));
</script>

<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-switch"
>
  <div class="flex items-center gap-1.5">
    <div class="flex flex-1 items-center justify-between gap-3">
      <div class="flex flex-col gap-0.5">
        <label
          class="text-sm font-medium"
          for={fieldId}>{label}</label
        >
        {#if description}
          <p class="text-muted-foreground text-xs">{description}</p>
        {/if}
      </div>
      <Switch
        id={fieldId}
        checked={currentValue}
        onCheckedChange={(checked) => (currentValue = checked)}
        disabled={saveState === "saving"}
      />
    </div>

    <InlineEditActions
      dirty={isDirty}
      {saveState}
      onSave={() => doSave()}
      onRevert={revert}
    />
  </div>

  {#if errorMessage}
    <p class="text-destructive text-xs">{errorMessage}</p>
  {/if}
</div>
