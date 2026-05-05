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

  interface FieldDef {
    key: string;
    label: string;
    value: string;
    placeholder?: string;
    maxLength?: number;
  }

  let {
    fields = $bindable([]),
    validate,
    onSave,
    onDirtyChange,
    class: className,
  }: {
    fields?: FieldDef[];
    validate?: (values: Record<string, string>) => Record<string, string> | undefined;
    onSave: (values: Record<string, string>) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  let originalValues: Record<string, string> = $state(
    Object.fromEntries(fields.map((f) => [f.key, f.value])),
  );
  let currentValues: Record<string, string> = $state(
    Object.fromEntries(fields.map((f) => [f.key, f.value])),
  );
  let saveState: SaveState = $state("idle");
  let fieldErrors: Record<string, string> = $state({});

  $effect(() => {
    const newOriginals = Object.fromEntries(fields.map((f) => [f.key, f.value]));
    originalValues = newOriginals;
    currentValues = { ...newOriginals };
  });

  const isDirty = $derived(
    fields.some((f) => (currentValues[f.key] ?? "").trim() !== originalValues[f.key]),
  );

  $effect(() => {
    onDirtyChange?.(isDirty);
  });

  export function revert() {
    currentValues = { ...originalValues };
    fieldErrors = {};
    saveState = "idle";
  }

  export function getDirty(): boolean {
    return isDirty;
  }

  export async function saveIfDirty(): Promise<boolean> {
    if (!isDirty) return true;
    return doSave();
  }

  function handleInput(key: string, e: Event) {
    currentValues[key] = (e.target as HTMLInputElement).value;
    if (fieldErrors[key]) {
      const { [key]: _, ...rest } = fieldErrors;
      fieldErrors = rest;
    }
    if (saveState === "saved" || saveState === "error") {
      saveState = "idle";
    }
  }

  async function doSave(): Promise<boolean> {
    const trimmed = Object.fromEntries(
      Object.entries(currentValues).map(([k, v]) => [k, v.trim()]),
    );

    if (validate) {
      const errors = validate(trimmed);
      if (errors) {
        fieldErrors = errors;
        return false;
      }
    }

    saveState = "saving";
    fieldErrors = {};
    try {
      await onSave(trimmed);
      for (const field of fields) {
        field.value = trimmed[field.key] ?? field.value;
      }
      originalValues = { ...trimmed };
      saveState = "saved";
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
      fieldErrors = { _form: err instanceof Error ? err.message : m.common_errors_SAVE_FAILED() };
      saveState = "error";
      return false;
    }
  }

  const handleKeydown = createInlineEditKeyHandler({
    isDirty: () => isDirty,
    onSave: () => doSave(),
    onRevert: revert,
  });
</script>

<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-edit-field-group"
>
  <div class="flex items-end gap-1.5">
    <div class="flex flex-1 flex-col gap-2 sm:flex-row">
      {#each fields as field (field.key)}
        {@const fieldDirty = (currentValues[field.key] ?? "").trim() !== originalValues[field.key]}
        <div class="flex flex-1 flex-col gap-1">
          <label
            class="text-sm font-medium"
            for={field.key}>{field.label}</label
          >
          <div class="relative">
            <input
              id={field.key}
              type="text"
              value={currentValues[field.key] ?? ""}
              placeholder={field.placeholder}
              maxlength={field.maxLength}
              disabled={saveState === "saving"}
              oninput={(e) => handleInput(field.key, e)}
              onkeydown={handleKeydown}
              class={cn(
                INPUT_CLASSES,
                INPUT_FOCUS_CLASSES,
                (fieldDirty || fieldErrors[field.key]) && "pr-8",
                fieldErrors[field.key] && INLINE_BORDER_INVALID,
                fieldDirty && !fieldErrors[field.key] && INLINE_BORDER_DIRTY,
                saveState === "saved" && INLINE_BORDER_SAVED,
              )}
            />
            {#if fieldDirty || fieldErrors[field.key]}
              <div class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2">
                <InlineFieldStatusIcon
                  saveState="idle"
                  validationStatus={fieldErrors[field.key]
                    ? "invalid"
                    : fieldDirty
                      ? "valid"
                      : "idle"}
                  dirty={fieldDirty}
                />
              </div>
            {/if}
          </div>
          {#if fieldErrors[field.key]}
            <p class="text-destructive text-xs">{fieldErrors[field.key]}</p>
          {/if}
        </div>
      {/each}
    </div>

    <InlineEditActions
      dirty={isDirty}
      {saveState}
      onSave={() => doSave()}
      onRevert={revert}
    />
  </div>

  {#if fieldErrors._form}
    <p class="text-destructive text-xs">{fieldErrors._form}</p>
  {/if}
</div>
