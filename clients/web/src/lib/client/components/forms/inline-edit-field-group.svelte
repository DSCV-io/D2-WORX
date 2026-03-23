<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Undo2Icon from "@lucide/svelte/icons/undo-2";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  type SaveState = "idle" | "saving" | "saved" | "error";

  const INPUT_CLASSES =
    "border-input bg-background placeholder:text-muted-foreground flex h-9 w-full min-w-0 rounded-md border px-3 py-1 text-base shadow-xs outline-none transition-[color,box-shadow,border-color] md:text-sm";
  const INPUT_FOCUS_CLASSES =
    "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]";

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

  // Sync when parent changes field values externally
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
      fieldErrors = { _form: err instanceof Error ? err.message : "Failed to save." };
      saveState = "error";
      return false;
    }
  }

  function handleKeydown(e: KeyboardEvent) {
    if (e.key === "Enter" && isDirty) {
      e.preventDefault();
      doSave();
    } else if (e.key === "Escape" && isDirty) {
      e.preventDefault();
      revert();
    }
  }
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
            for={field.key}
          >
            {field.label}
          </label>
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
              fieldErrors[field.key] && "border-destructive",
              fieldDirty && !fieldErrors[field.key] && "border-blue-500/50",
              saveState === "saved" && "border-green-500/50",
            )}
          />
          {#if fieldErrors[field.key]}
            <p class="text-destructive text-xs">{fieldErrors[field.key]}</p>
          {/if}
        </div>
      {/each}
    </div>

    <!-- Action buttons -->
    <div class="flex w-[4.625rem] shrink-0 justify-center gap-0.5 pb-px">
      {#if saveState === "saving"}
        <div class="flex size-9 items-center justify-center">
          <LoaderCircleIcon class="text-muted-foreground size-4 animate-spin" />
        </div>
      {:else if saveState === "saved"}
        <div class="flex size-9 items-center justify-center">
          <CircleCheckIcon class="size-4 text-green-500" />
        </div>
      {:else if isDirty}
        <button
          type="button"
          onclick={() => doSave()}
          class="text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 cursor-pointer items-center justify-center rounded-md transition-colors"
        >
          <SaveIcon class="size-4" />
          <span class="sr-only">Save</span>
        </button>
        <button
          type="button"
          onclick={revert}
          class="text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 cursor-pointer items-center justify-center rounded-md transition-colors"
        >
          <Undo2Icon class="size-4" />
          <span class="sr-only">Revert</span>
        </button>
      {/if}
    </div>
  </div>

  {#if fieldErrors._form}
    <p class="text-destructive text-xs">{fieldErrors._form}</p>
  {/if}
</div>
