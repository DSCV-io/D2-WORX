<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import {
    INPUT_CLASSES,
    INPUT_FOCUS_CLASSES,
    INLINE_BORDER_DIRTY,
    INLINE_BORDER_SAVED,
    INLINE_BORDER_INVALID,
  } from "$lib/shared/forms/input-styles.js";
  import { createInlineEditKeyHandler } from "$lib/shared/forms/inline-edit-keyboard.js";
  import { isSaveCanceledError } from "$lib/shared/forms/save-canceled-error.js";
  import * as m from "$lib/paraglide/messages.js";
  import InlineEditActions from "./inline-edit-actions.svelte";
  import InlineFieldStatusIcon from "./inline-field-status-icon.svelte";

  type SaveState = "idle" | "saving" | "saved" | "error";
  type ValidationStatus = "idle" | "validating" | "valid" | "invalid";

  let {
    value = $bindable(""),
    label,
    placeholder = "",
    maxLength,
    validate,
    asyncValidate,
    onSave,
    onDirtyChange,
    class: className,
  }: {
    value?: string;
    label: string;
    placeholder?: string;
    maxLength?: number;
    validate?: (value: string) => string | undefined;
    asyncValidate?: (value: string) => Promise<string | undefined>;
    onSave: (value: string) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  let originalValue = $state(value);
  let currentValue = $state(value);
  let saveState = $state<SaveState>("idle");
  let validationStatus = $state<ValidationStatus>("idle");
  let errorMessage = $state("");
  let blurred = $state(false);

  $effect(() => {
    originalValue = value;
    currentValue = value;
  });

  const isDirty = $derived(currentValue.trim() !== originalValue);

  $effect(() => {
    onDirtyChange?.(isDirty);
  });

  export function revert() {
    currentValue = originalValue;
    errorMessage = "";
    saveState = "idle";
    validationStatus = "idle";
    blurred = false;
  }

  export function getDirty(): boolean {
    return isDirty;
  }

  export async function saveIfDirty(): Promise<boolean> {
    if (!isDirty) return true;
    return doSave();
  }

  function handleInput(e: Event) {
    currentValue = (e.target as HTMLInputElement).value;
    if (blurred) {
      errorMessage = "";
      validationStatus = "idle";
      blurred = false;
    }
    if (saveState === "saved" || saveState === "error") {
      saveState = "idle";
    }
  }

  async function handleBlur() {
    blurred = true;
    const trimmed = currentValue.trim();

    if (validate) {
      const err = validate(trimmed);
      if (err) {
        errorMessage = err;
        validationStatus = "invalid";
        return;
      }
    }

    if (asyncValidate && isDirty) {
      validationStatus = "validating";
      errorMessage = "";
      const err = await asyncValidate(trimmed);
      if (err) {
        errorMessage = err;
        validationStatus = "invalid";
        return;
      }
    }

    if (isDirty && trimmed) {
      validationStatus = "valid";
      errorMessage = "";
    } else if (!isDirty) {
      validationStatus = "idle";
      errorMessage = "";
    }
  }

  async function doSave(): Promise<boolean> {
    const trimmed = currentValue.trim();

    if (validate) {
      const err = validate(trimmed);
      if (err) {
        errorMessage = err;
        validationStatus = "invalid";
        return false;
      }
    }

    saveState = "saving";
    errorMessage = "";
    try {
      await onSave(trimmed);
      value = trimmed;
      originalValue = trimmed;
      saveState = "saved";
      validationStatus = "idle";
      blurred = false;
      setTimeout(() => {
        if (saveState === "saved") saveState = "idle";
      }, 2000);
      return true;
    } catch (err) {
      // User-canceled flow (e.g., dismissed a confirmation modal). Stay
      // dirty so save/revert reappear; do NOT show an error.
      if (isSaveCanceledError(err)) {
        saveState = "idle";
        return false;
      }
      errorMessage = err instanceof Error ? err.message : m.common_errors_SAVE_FAILED();
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

  // Inner status icon shows for validation feedback only.
  // Save lifecycle (saving/saved) is handled by InlineEditActions to the right —
  // duplicating it here would put two checkmarks side-by-side after a save.
  // For "error", validationStatus is also "invalid", so the inner X still shows.
  const showStatusIcon = $derived(
    saveState === "error" ||
      (blurred && validationStatus !== "idle") ||
      (isDirty && validationStatus === "idle"),
  );

  const fieldId = $derived(label.toLowerCase().replace(/\s+/g, "-"));
</script>

<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-edit-field"
>
  <label
    class="text-sm font-medium"
    for={fieldId}>{label}</label
  >

  <div class="flex items-center gap-1.5">
    <div class="relative flex-1">
      <input
        id={fieldId}
        type="text"
        value={currentValue}
        {placeholder}
        maxlength={maxLength}
        disabled={saveState === "saving"}
        oninput={handleInput}
        onblur={handleBlur}
        onkeydown={handleKeydown}
        class={cn(
          INPUT_CLASSES,
          INPUT_FOCUS_CLASSES,
          showStatusIcon ? "pr-8" : "",
          saveState === "saved" && INLINE_BORDER_SAVED,
          validationStatus === "invalid" && INLINE_BORDER_INVALID,
          isDirty && validationStatus !== "invalid" && saveState !== "saved" && INLINE_BORDER_DIRTY,
        )}
      />

      {#if showStatusIcon}
        <div class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2">
          <InlineFieldStatusIcon
            {saveState}
            {validationStatus}
            dirty={isDirty}
          />
        </div>
      {/if}
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
