<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Undo2Icon from "@lucide/svelte/icons/undo-2";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import CircleXIcon from "@lucide/svelte/icons/circle-x";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  type SaveState = "idle" | "saving" | "saved" | "error";
  type ValidationStatus = "idle" | "validating" | "valid" | "invalid";

  const INPUT_CLASSES =
    "border-input bg-background placeholder:text-muted-foreground flex h-9 w-full min-w-0 rounded-md border px-3 py-1 text-base shadow-xs outline-none transition-[color,box-shadow,border-color] md:text-sm";
  const INPUT_FOCUS_CLASSES =
    "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]";

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
    /** Synchronous validation — returns error message or undefined. */
    validate?: (value: string) => string | undefined;
    /** Async validation (e.g., username availability) — runs on blur when sync passes. */
    asyncValidate?: (value: string) => Promise<string | undefined>;
    onSave: (value: string) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  let originalValue = $state(value);
  let currentValue = $state(value);
  let saveState: SaveState = $state("idle");
  let validationStatus: ValidationStatus = $state("idle");
  let errorMessage = $state("");
  let blurred = $state(false);

  // Sync when the parent changes the bound value externally
  $effect(() => {
    originalValue = value;
    currentValue = value;
  });

  const isDirty = $derived(currentValue.trim() !== originalValue);

  // Notify parent of dirty state changes
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
      errorMessage = err instanceof Error ? err.message : "Failed to save.";
      saveState = "error";
      validationStatus = "invalid";
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

  const showStatusIcon = $derived(
    (saveState as string) !== "idle" ||
      (blurred && validationStatus !== "idle") ||
      (isDirty && validationStatus === "idle"),
  );
</script>

<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-edit-field"
>
  <label
    class="text-sm font-medium"
    for={label.toLowerCase().replace(/\s+/g, "-")}
  >
    {label}
  </label>

  <div class="flex items-center gap-1.5">
    <div class="relative flex-1">
      <input
        id={label.toLowerCase().replace(/\s+/g, "-")}
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
          saveState === "saved" && "border-green-500/50",
          validationStatus === "invalid" && "border-destructive",
          isDirty &&
            validationStatus !== "invalid" &&
            saveState !== "saved" &&
            "border-blue-500/50",
        )}
      />

      {#if showStatusIcon}
        <div class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2">
          {#if saveState === "saving"}
            <LoaderCircleIcon class="text-muted-foreground size-3.5 animate-spin" />
          {:else if saveState === "saved"}
            <CircleCheckIcon class="size-3.5 text-green-500" />
          {:else if validationStatus === "validating"}
            <LoaderCircleIcon class="text-muted-foreground size-3.5 animate-spin" />
          {:else if validationStatus === "invalid"}
            <CircleXIcon class="text-destructive size-3.5" />
          {:else if validationStatus === "valid" || isDirty}
            <CircleCheckIcon class="size-3.5 text-blue-500" />
          {/if}
        </div>
      {/if}
    </div>

    <!-- Save / Revert buttons — visible when dirty or saving -->
    <div class="flex w-[4.625rem] shrink-0 justify-center gap-0.5">
      {#if saveState === "saving"}
        <div class="flex size-9 items-center justify-center">
          <LoaderCircleIcon class="text-muted-foreground size-4 animate-spin" />
        </div>
      {:else if isDirty}
        <button
          type="button"
          onclick={() => doSave()}
          disabled={validationStatus === "invalid"}
          class={cn(
            "text-muted-foreground hover:text-foreground hover:bg-muted flex size-9 cursor-pointer items-center justify-center rounded-md transition-colors",
            validationStatus === "invalid" && "cursor-not-allowed opacity-50",
          )}
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

  {#if errorMessage}
    <p class="text-destructive text-xs">{errorMessage}</p>
  {/if}
</div>
