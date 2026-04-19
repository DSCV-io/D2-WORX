<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import * as Command from "$lib/client/components/ui/command/index.js";
  import { buttonVariants } from "$lib/client/components/ui/button/index.js";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import type { CountryOption } from "$lib/shared/forms/geo-ref-data.js";
  import { formatPhoneAsYouType, getCountryCallingCode } from "$lib/shared/forms/phone-format.js";
  import FieldStatusIcon from "./field-status-icon.svelte";
  import { getFieldStatus } from "$lib/shared/forms/field-status.js";
  import * as m from "$lib/paraglide/messages.js";

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    countries: CountryOption[];
    defaultCountry?: string;
    description?: string;
    disabled?: boolean;
  };

  let {
    form,
    field,
    label,
    countries,
    defaultCountry = "US",
    description,
    disabled = false,
  }: Props = $props();

  let countryOpen = $state(false);
  let selectedCountryCode = $state(initDefault());
  function initDefault() {
    return defaultCountry;
  }
  /** The formatted display value the user sees. */
  let displayValue = $state("");
  let showStatus = $state(false);

  const formData = $derived(form.form);
  const errors = $derived(form.errors);
  const constraints = $derived(form.constraints);

  const fieldErrors = $derived(($errors as Record<string, string[]>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );
  const phoneValue = $derived(($formData as Record<string, string>)[field as string] ?? "");
  const fieldStatus = $derived(
    showStatus ? getFieldStatus({ errors: fieldErrors, value: phoneValue }) : "idle",
  );
  const selectedCountry = $derived(countries.find((c) => c.value === selectedCountryCode));
  const prefix = $derived(
    selectedCountry?.phonePrefix || getCountryCallingCode(selectedCountryCode),
  );

  function handleCountrySelect(code: string) {
    selectedCountryCode = code;
    countryOpen = false;
    // Re-format existing digits for new country + update stored E.164
    updateFormValue(displayValue);
  }

  function handlePhoneInput(e: Event & { currentTarget: HTMLInputElement }) {
    const raw = e.currentTarget.value;
    // Clear status while typing
    if (showStatus) {
      showStatus = false;
      errors.update((errs) => {
        const copy = { ...errs } as Record<string, unknown>;
        delete copy[field as string];
        return copy as typeof errs;
      });
    }
    updateFormValue(raw);
  }

  function handleBlur() {
    (form.validate as (path: string) => Promise<unknown>)(field as string).then(() => {
      showStatus = true;
    });
  }

  function updateFormValue(raw: string) {
    if (!raw.trim()) {
      displayValue = "";
      ($formData as Record<string, string>)[field as string] = "";
      return;
    }
    const result = formatPhoneAsYouType(raw, selectedCountryCode);
    displayValue = result.formatted;
    // Store full E.164 in form data
    ($formData as Record<string, string>)[field as string] = result.national
      ? `${prefix}${result.national}`
      : "";
  }
</script>

<Form.Field
  {form}
  name={field}
>
  <Control>
    {#snippet children({ props })}
      {@const { name: fieldName, ...triggerProps } = props}
      <Form.Label>
        {label}
        {#if isRequired}<span
            class="text-destructive"
            aria-hidden="true">*</span
          >{/if}
      </Form.Label>
      <input
        type="hidden"
        name={fieldName}
        value={($formData as Record<string, string>)[field as string] ?? ""}
      />
      <div class="flex gap-1">
        <!-- Country prefix selector -->
        <Popover.Root bind:open={countryOpen}>
          <Popover.Trigger
            {disabled}
            class={cn(
              buttonVariants({ variant: "outline" }),
              "flex w-[100px] shrink-0 items-center gap-1 px-2",
            )}
          >
            {#if selectedCountry}
              <img
                src={selectedCountry.flag}
                alt={selectedCountry.label}
                class="h-3 w-4 shrink-0 object-cover"
              />
            {/if}
            <span class="text-muted-foreground text-xs">{prefix}</span>
            <ChevronsUpDownIcon class="ml-auto size-3 shrink-0 opacity-50" />
          </Popover.Trigger>
          <Popover.Content
            class="w-[280px] p-0"
            align="start"
          >
            <Command.Root>
              <Command.Input placeholder={m.webclient_forms_country_search()} />
              <Command.List>
                <Command.Empty>{m.webclient_forms_country_empty()}</Command.Empty>
                <Command.Group>
                  {#each countries as country (country.value)}
                    <Command.Item
                      value={country.value}
                      keywords={[country.label, country.phonePrefix]}
                      onSelect={() => handleCountrySelect(country.value)}
                    >
                      <CheckIcon
                        class="mr-2 size-4 shrink-0 {selectedCountryCode === country.value
                          ? 'opacity-100'
                          : 'opacity-0'}"
                      />
                      <img
                        src={country.flag}
                        alt=""
                        class="mr-2 h-3 w-4 shrink-0 object-cover"
                      />
                      <span class="truncate">{country.label}</span>
                      <span class="ml-auto text-xs opacity-70">
                        {country.phonePrefix}
                      </span>
                    </Command.Item>
                  {/each}
                </Command.Group>
              </Command.List>
            </Command.Root>
          </Popover.Content>
        </Popover.Root>

        <!-- Phone number input (display only — hidden input carries the E.164 value) -->
        <div class="relative flex-1">
          <Input
            {...triggerProps}
            type="tel"
            autocomplete="off"
            value={displayValue}
            placeholder={m.account_phone_input_label()}
            {disabled}
            oninput={handlePhoneInput}
            onblur={handleBlur}
            class={cn(
              "w-full",
              fieldStatus !== "idle" && "pr-8",
              fieldStatus === "valid" && "border-success/70 dark:border-success/50",
            )}
          />
          {#if fieldStatus !== "idle"}
            <div class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2">
              <FieldStatusIcon status={fieldStatus} />
            </div>
          {/if}
        </div>
      </div>
    {/snippet}
  </Control>
  {#if description}
    <Form.Description>{description}</Form.Description>
  {/if}
  <Form.FieldErrors />
</Form.Field>
