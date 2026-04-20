<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import * as Select from "$lib/client/components/ui/select/index.js";
  import { createInlineEditKeyHandler } from "$lib/shared/forms/inline-edit-keyboard.js";
  import { isSaveCancelledError } from "$lib/shared/forms/save-cancelled-error.js";
  import InlineEditActions from "./inline-edit-actions.svelte";
  import InlineFieldStatusIcon from "./inline-field-status-icon.svelte";

  type SaveState = "idle" | "saving" | "saved" | "error";
  type ValidationStatus = "idle" | "valid" | "invalid";

  interface Option {
    value: string;
    label: string;
    /** Optional image URL (e.g., flag SVG). */
    image?: string;
  }

  let {
    value = $bindable(""),
    label,
    options,
    validate,
    onSave,
    onDirtyChange,
    class: className,
  }: {
    value?: string;
    label: string;
    options: Option[];
    validate?: (value: string) => string | undefined;
    onSave: (value: string) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  let originalValue = $state(value);
  let currentValue = $state(value);
  let saveState: SaveState = $state("idle");
  let validationStatus: ValidationStatus = $state("idle");
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
    validationStatus = "idle";
  }

  export function getDirty(): boolean {
    return isDirty;
  }

  export async function saveIfDirty(): Promise<boolean> {
    if (!isDirty) return true;
    return doSave();
  }

  function handleValueChange(newValue: string) {
    currentValue = newValue;
    if (saveState === "saved" || saveState === "error") {
      saveState = "idle";
    }

    // Validate immediately on change
    if (validate) {
      const err = validate(newValue);
      if (err) {
        errorMessage = err;
        validationStatus = "invalid";
        return;
      }
    }

    if (newValue !== originalValue) {
      validationStatus = "valid";
      errorMessage = "";
    } else {
      validationStatus = "idle";
      errorMessage = "";
    }
  }

  async function doSave(): Promise<boolean> {
    if (validate) {
      const err = validate(currentValue);
      if (err) {
        errorMessage = err;
        validationStatus = "invalid";
        return false;
      }
    }

    saveState = "saving";
    errorMessage = "";
    try {
      await onSave(currentValue);
      value = currentValue;
      originalValue = currentValue;
      saveState = "saved";
      validationStatus = "idle";
      setTimeout(() => {
        if (saveState === "saved") saveState = "idle";
      }, 2000);
      return true;
    } catch (err) {
      // User-cancelled flow (e.g., dismissed a confirmation modal). Stay
      // dirty so save/revert reappear; do NOT show an error.
      if (isSaveCancelledError(err)) {
        saveState = "idle";
        return false;
      }
      errorMessage = err instanceof Error ? err.message : "Failed to save.";
      saveState = "error";
      validationStatus = "invalid";
      return false;
    }
  }

  const handleKeydown = createInlineEditKeyHandler({
    isDirty: () => isDirty,
    onSave: () => doSave(),
    onRevert: revert,
  });

  const showStatusIcon = $derived(validationStatus !== "idle" || isDirty);

  const fieldId = $derived(label.toLowerCase().replace(/\s+/g, "-"));
  const selectedOption = $derived(options.find((o) => o.value === currentValue));
</script>

<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-dropdown"
  onkeydown={handleKeydown}
>
  <label
    class="text-sm font-medium"
    for={fieldId}>{label}</label
  >

  <div class="flex items-center gap-1.5">
    <div class="relative flex-1">
      <Select.Root
        type="single"
        value={currentValue}
        onValueChange={handleValueChange}
        disabled={saveState === "saving"}
      >
        <Select.Trigger
          id={fieldId}
          class={cn(
            "w-full",
            saveState === "saved" && "border-green-500/50",
            validationStatus === "invalid" && "border-destructive",
            isDirty &&
              validationStatus !== "invalid" &&
              saveState !== "saved" &&
              "border-blue-500/50",
          )}
        >
          {#if selectedOption?.image}
            <img
              src={selectedOption.image}
              alt=""
              class="h-3 w-4 shrink-0 object-cover"
            />
          {/if}
          <span class="flex-1 text-left">{selectedOption?.label || "Select..."}</span>
          {#if showStatusIcon}
            <InlineFieldStatusIcon
              saveState="idle"
              {validationStatus}
              dirty={isDirty}
            />
          {/if}
        </Select.Trigger>
        <Select.Content>
          {#each options as option (option.value)}
            <Select.Item value={option.value}>
              {#if option.image}
                <img
                  src={option.image}
                  alt=""
                  class="h-3 w-4 shrink-0 object-cover"
                />
              {/if}
              {option.label}
            </Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
    </div>

    <InlineEditActions
      dirty={isDirty}
      {saveState}
      saveDisabled={validationStatus === "invalid"}
      onSave={() => doSave()}
      onRevert={revert}
    />
  </div>

  {#if errorMessage}
    <p class="text-destructive text-xs">{errorMessage}</p>
  {/if}
</div>
