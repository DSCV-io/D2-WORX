<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import { Combobox } from "bits-ui";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import ChevronUpIcon from "@lucide/svelte/icons/chevron-up";
  import ChevronDownIcon from "@lucide/svelte/icons/chevron-down";
  import FieldStatusIcon from "./field-status-icon.svelte";
  import { getFieldStatus } from "$lib/shared/forms/field-status.js";

  type Option = { value: string; label: string; flag?: string };

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    options: Option[];
    placeholder?: string;
    searchPlaceholder?: string;
    emptyMessage?: string;
    description?: string;
    disabled?: boolean;
    onValueChange?: (value: string) => void;
  };

  let {
    form,
    field,
    label,
    options,
    placeholder = "Search...",
    searchPlaceholder: _searchPlaceholder,
    emptyMessage = "No results found.",
    description,
    disabled = false,
    onValueChange,
  }: Props = $props();

  let searchValue = $state("");
  let open = $state(false);
  let showStatus = $state(false);

  const formData = $derived(form.form);
  const errors = $derived(form.errors);
  const constraints = $derived(form.constraints);

  let value = $derived(($formData as Record<string, string>)[field as string] ?? "");
  const selectedOption = $derived(options.find((o) => o.value === value));
  const fieldErrors = $derived(($errors as Record<string, string[]>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );
  const fieldStatus = $derived(
    showStatus ? getFieldStatus({ errors: fieldErrors, value }) : "idle",
  );

  const filteredOptions = $derived(
    searchValue
      ? options.filter((o) => o.label.toLowerCase().includes(searchValue.toLowerCase()))
      : options,
  );

  function handleValueChange(newValue: string) {
    ($formData as Record<string, string>)[field as string] = newValue;
    searchValue = "";
    onValueChange?.(newValue);
  }

  function handleInput(e: Event & { currentTarget: HTMLInputElement }) {
    searchValue = e.currentTarget.value;
  }

  function handleOpenChangeComplete(isOpen: boolean) {
    if (!isOpen) {
      searchValue = "";
      // Validate on close to show status
      (form.validate as (path: string) => Promise<unknown>)(field as string).then(() => {
        showStatus = true;
      });
    }
  }
</script>

<Form.Field
  {form}
  name={field}
>
  <Control>
    {#snippet children({ props })}
      {@const { name: fieldName, ...inputProps } = props}
      <Form.Label>
        {label}
        {#if isRequired}<span
            class="text-destructive"
            aria-hidden="true">*</span
          >{/if}
        <FieldStatusIcon status={fieldStatus} />
      </Form.Label>
      <input
        type="hidden"
        name={fieldName}
        {value}
        autocomplete="off"
      />
      <Combobox.Root
        type="single"
        {disabled}
        bind:open
        {value}
        onValueChange={handleValueChange}
        items={options.map((o) => ({ value: o.value, label: o.label }))}
        onOpenChangeComplete={handleOpenChangeComplete}
      >
        <div class="relative">
          {#if selectedOption?.flag}
            <img
              src={selectedOption.flag}
              alt=""
              class="absolute top-1/2 left-2.5 z-10 h-3 w-4 -translate-y-1/2 object-cover"
            />
          {/if}
          <Combobox.Input
            {...inputProps}
            autocomplete="off"
            oninput={handleInput}
            onfocusin={() => {
              open = true;
            }}
            onclick={() => {
              open = true;
            }}
            {placeholder}
            class={cn(
              "border-input bg-input placeholder:text-muted-foreground flex h-9 w-full min-w-0 rounded-md border px-3 py-1 pr-8 text-base transition-[color,box-shadow] outline-none md:text-sm",
              "focus-visible:border-ring focus-visible:ring-ring/50 focus-visible:ring-[3px]",
              "aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-2 aria-invalid:border-destructive",
              "disabled:cursor-not-allowed disabled:opacity-50",
              selectedOption?.flag && "pl-8",
              fieldStatus === "valid" && "border-2 border-success dark:border-success",
              fieldStatus === "invalid" && "border-2",
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
            class="bg-popover text-popover-foreground data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-end-2 data-[side=right]:slide-in-from-start-2 data-[side=top]:slide-in-from-bottom-2 relative z-50 max-h-(--bits-combobox-content-available-height) min-w-[8rem] origin-(--bits-combobox-content-transform-origin) overflow-x-hidden overflow-y-auto rounded-md border shadow-md data-[side=bottom]:translate-y-1 data-[side=left]:-translate-x-1 data-[side=right]:translate-x-1 data-[side=top]:-translate-y-1"
          >
            <Combobox.ScrollUpButton class="flex cursor-default items-center justify-center py-1">
              <ChevronUpIcon class="size-4" />
            </Combobox.ScrollUpButton>
            <Combobox.Viewport class="w-full min-w-(--bits-combobox-anchor-width) scroll-my-1 p-1">
              {#each filteredOptions as option (option.value)}
                <Combobox.Item
                  value={option.value}
                  label={option.label}
                  class="data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground [&_svg:not([class*='text-'])]:text-muted-foreground data-[highlighted]:[&_svg:not([class*='text-'])]:text-accent-foreground relative flex w-full cursor-pointer items-center gap-2 rounded-sm py-1.5 ps-2 pe-8 text-sm outline-hidden select-none data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4"
                >
                  {#snippet children({ selected })}
                    {#if option.flag}
                      <img
                        src={option.flag}
                        alt=""
                        class="h-3 w-4 shrink-0 object-cover"
                      />
                    {/if}
                    {option.label}
                    <span class="absolute end-2 flex size-3.5 items-center justify-center">
                      {#if selected}
                        <CheckIcon class="size-4" />
                      {/if}
                    </span>
                  {/snippet}
                </Combobox.Item>
              {:else}
                <div class="py-6 text-center text-sm text-muted-foreground">
                  {emptyMessage}
                </div>
              {/each}
            </Combobox.Viewport>
            <Combobox.ScrollDownButton class="flex cursor-default items-center justify-center py-1">
              <ChevronDownIcon class="size-4" />
            </Combobox.ScrollDownButton>
          </Combobox.Content>
        </Combobox.Portal>
      </Combobox.Root>
    {/snippet}
  </Control>
  {#if description}
    <Form.Description>{description}</Form.Description>
  {/if}
  <Form.FieldErrors />
</Form.Field>
