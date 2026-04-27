<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import { Combobox } from "bits-ui";
  import { createInlineEditKeyHandler } from "$lib/shared/forms/inline-edit-keyboard.js";
  import { isSaveCancelledError } from "$lib/shared/forms/save-cancelled-error.js";
  import * as m from "$lib/paraglide/messages.js";
  import InlineEditActions from "./inline-edit-actions.svelte";
  import InlineFieldStatusIcon from "./inline-field-status-icon.svelte";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import ChevronUpIcon from "@lucide/svelte/icons/chevron-up";
  import ChevronDownIcon from "@lucide/svelte/icons/chevron-down";

  type SaveState = "idle" | "saving" | "saved" | "error";
  type ValidationStatus = "idle" | "valid" | "invalid";

  interface Option {
    value: string;
    label: string;
  }

  let {
    value = $bindable(""),
    label,
    options,
    placeholder,
    emptyMessage,
    validate,
    onSave,
    onDirtyChange,
    class: className,
  }: {
    value?: string;
    label: string;
    options: Option[];
    placeholder?: string;
    emptyMessage?: string;
    validate?: (value: string) => string | undefined;
    onSave: (value: string) => Promise<void>;
    onDirtyChange?: (dirty: boolean) => void;
    class?: string;
  } = $props();

  // Defaults pulled from Paraglide so the component is i18n-safe even when
  // callers omit the optional placeholder/emptyMessage props.
  const placeholderText = $derived(placeholder ?? m.common_ui_search_placeholder());
  const emptyMessageText = $derived(emptyMessage ?? m.common_ui_no_results());

  let originalValue = $state(value);
  let currentValue = $state(value);
  let saveState = $state<SaveState>("idle");
  let validationStatus = $state<ValidationStatus>("idle");
  let errorMessage = $state("");
  let searchValue = $state("");
  let open = $state(false);
  // svelte-ignore state_referenced_locally
  // Initial-only seed; the $effect below keeps it in sync with displayLabel.
  let comboboxInputValue = $state(options.find((o) => o.value === value)?.label ?? "");

  /** The label to display in the input when closed. Reactively derived from currentValue + options. */
  const displayLabel = $derived(options.find((o) => o.value === currentValue)?.label ?? "");

  // Sync comboboxInputValue when value or options change (e.g., async load)
  $effect(() => {
    if (!open) {
      comboboxInputValue = displayLabel;
    }
  });

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

  const filteredOptions = $derived(
    searchValue
      ? options.filter((o) => o.label.toLowerCase().includes(searchValue.toLowerCase()))
      : options,
  );

  const selectedOption = $derived(options.find((o) => o.value === currentValue));

  function handleValueChange(newValue: string) {
    currentValue = newValue;
    searchValue = "";
    if (saveState === "saved" || saveState === "error") {
      saveState = "idle";
    }

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
      errorMessage = err instanceof Error ? err.message : m.common_errors_save_failed();
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
</script>

<!-- svelte-ignore a11y_no_static_element_interactions -->
<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-combobox"
  onkeydown={handleKeydown}
>
  <label
    class="text-sm font-medium"
    for={fieldId}>{label}</label
  >

  <div class="flex items-center gap-1.5">
    <div class="relative flex-1">
      <Combobox.Root
        type="single"
        disabled={saveState === "saving"}
        bind:open
        inputValue={comboboxInputValue}
        value={currentValue}
        onValueChange={handleValueChange}
        items={options.map((o) => ({ value: o.value, label: o.label }))}
        onOpenChangeComplete={(isOpen) => {
          if (!isOpen) {
            searchValue = "";
            comboboxInputValue = displayLabel;
          }
        }}
      >
        <div class="relative">
          <Combobox.Input
            id={fieldId}
            autocomplete="off"
            oninput={(e) => {
              searchValue = (e.target as HTMLInputElement).value;
            }}
            onfocusin={() => {
              open = true;
              searchValue = "";
            }}
            onclick={() => {
              open = true;
              searchValue = "";
            }}
            placeholder={placeholderText}
            class={cn(
              "border-input bg-input placeholder:text-muted-foreground flex h-9 w-full min-w-0 rounded-md border px-3 py-1 pr-8 text-base transition-[color,box-shadow] outline-none md:text-sm",
              "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]",
              "disabled:cursor-not-allowed disabled:opacity-50",
              saveState === "saved" && "border-2 border-green-500",
              validationStatus === "invalid" && "border-destructive border-2",
              isDirty &&
                validationStatus !== "invalid" &&
                saveState !== "saved" &&
                "border-2 border-blue-500",
            )}
          />
          <Combobox.Trigger class="absolute top-1/2 right-2 -translate-y-1/2">
            <ChevronsUpDownIcon class="size-4 opacity-50" />
          </Combobox.Trigger>
        </div>
        <Combobox.Portal>
          <Combobox.Content
            sideOffset={4}
            preventScroll={true}
            class="bg-popover text-popover-foreground data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 relative z-50 max-h-(--bits-combobox-content-available-height) min-w-[8rem] origin-(--bits-combobox-content-transform-origin) overflow-x-hidden overflow-y-auto rounded-md border shadow-md data-[side=bottom]:translate-y-1"
          >
            <Combobox.ScrollUpButton class="flex cursor-default items-center justify-center py-1">
              <ChevronUpIcon class="size-4" />
            </Combobox.ScrollUpButton>
            <Combobox.Viewport class="w-full min-w-(--bits-combobox-anchor-width) scroll-my-1 p-1">
              {#each filteredOptions as option (option.value)}
                <Combobox.Item
                  value={option.value}
                  label={option.label}
                  class="data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground relative flex w-full cursor-pointer items-center gap-2 rounded-sm py-1.5 ps-2 pe-8 text-sm outline-hidden select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50"
                >
                  {#snippet children({ selected })}
                    {option.label}
                    <span class="absolute end-2 flex size-3.5 items-center justify-center">
                      {#if selected}
                        <CheckIcon class="size-4" />
                      {/if}
                    </span>
                  {/snippet}
                </Combobox.Item>
              {:else}
                <div class="text-muted-foreground py-6 text-center text-sm">
                  {emptyMessageText}
                </div>
              {/each}
            </Combobox.Viewport>
            <Combobox.ScrollDownButton class="flex cursor-default items-center justify-center py-1">
              <ChevronDownIcon class="size-4" />
            </Combobox.ScrollDownButton>
          </Combobox.Content>
        </Combobox.Portal>
      </Combobox.Root>

      {#if showStatusIcon}
        <div class="pointer-events-none absolute top-1/2 right-8 -translate-y-1/2">
          <InlineFieldStatusIcon
            saveState="idle"
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
