<script lang="ts">
  import { resolve } from "$app/paths";
  import { superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { untrack } from "svelte";
  import { createContactSchema } from "$lib/shared/forms/contact-schema.js";
  import { FormInput } from "$lib/client/components/forms/index.js";
  import FormCombobox from "$lib/client/components/forms/form-combobox.svelte";
  import FormPhoneInput from "$lib/client/components/forms/form-phone-input.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import {
    FIRST_NAME,
    LAST_NAME,
    EMAIL,
    PHONE,
    COUNTRY,
    STATE,
    STREET1,
    STREET2,
    STREET3,
    CITY,
    POSTAL_CODE,
  } from "$lib/shared/forms/field-presets.js";
  import { useCountryState } from "$lib/client/forms/country-state.svelte.js";
  import { useAddressLines } from "$lib/client/forms/address-lines.svelte.js";
  import { useAsyncFieldCheck } from "$lib/client/forms/async-field-check.svelte.js";
  import { maskDisplayName } from "$lib/client/utils/mask-display-name.js";
  import ArrowLeftIcon from "@lucide/svelte/icons/arrow-left";
  import * as m from "$lib/paraglide/messages.js";

  let { data } = $props();

  // Page data is stable for the component lifetime (navigation remounts).
  // Use untrack to read the initial values without subscribing to changes.
  const {
    form: formDefaults,
    countries,
    subdivisionsByCountry,
    countriesWithSubdivisions,
  } = untrack(() => data);

  const schema = createContactSchema(new Set(countriesWithSubdivisions));

  const form = superForm(formDefaults, {
    id: "contact-form",
    validators: zodClient(schema),
    onUpdated({ form: f }) {
      if (f.valid && f.message) {
        toast.success(f.message as string);
      }
    },
  });

  const { enhance } = form;

  // Composable returns use getter-based reactivity — do NOT destructure
  // (destructuring evaluates getters once and loses $state tracking).
  const countryState = useCountryState({ form, subdivisionsByCountry });
  const addressLines = useAddressLines({ form });

  const emailCheck = useAsyncFieldCheck({
    form,
    field: "email",
    preCheck: (v) => !!v && v.includes("@"),
    async checker(email) {
      const res = await fetch(`/debug/design/api/check-email?email=${encodeURIComponent(email)}`);
      const { available } = await res.json();
      return { valid: available, errorMessage: "This email is already taken" };
    },
  });
</script>

<svelte:head>
  <title>{m.webclient_design_contact_form_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_design_contact_form_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
  <meta
    property="og:title"
    content="{m.webclient_design_contact_form_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_design_contact_form_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="mx-auto max-w-2xl px-4 py-8">
  <!-- Header -->
  <div class="mb-6">
    <a
      href={resolve("/debug/design")}
      class="text-muted-foreground hover:text-foreground mb-4 inline-flex items-center gap-1 text-sm"
    >
      <ArrowLeftIcon class="size-4" />
      {m.webclient_design_back_to_design()}
    </a>
    <h1 class="text-2xl font-bold tracking-tight">{m.webclient_design_contact_form_heading()}</h1>
    <p class="text-muted-foreground mt-1 text-sm">
      {m.webclient_design_contact_form_demo_description()}
    </p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title>{m.webclient_design_contact_new()}</Card.Title>
      <Card.Description>
        {m.webclient_design_contact_cache_description()}
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <form
        method="POST"
        use:enhance
        autocomplete="off"
        class="flex flex-col gap-5"
      >
        <!-- Name row -->
        <div class="grid gap-4 sm:grid-cols-2">
          <FormInput
            {form}
            field="firstName"
            {...FIRST_NAME}
            oninput={maskDisplayName}
          />
          <FormInput
            {form}
            field="lastName"
            {...LAST_NAME}
            oninput={maskDisplayName}
          />
        </div>

        <!-- Email with async availability check -->
        <FormInput
          {form}
          field="email"
          {...EMAIL}
          status={emailCheck.status === "idle" ? undefined : emailCheck.status}
          onblur={emailCheck.check}
          oninput={emailCheck.reset}
        />

        <!-- Phone with country selector -->
        <FormPhoneInput
          {form}
          field="phone"
          label={PHONE.label}
          {countries}
          defaultCountry="US"
          description={PHONE.description}
        />

        <!-- Country combobox -->
        <FormCombobox
          {form}
          field="country"
          {...COUNTRY}
          options={countries}
          onValueChange={countryState.handleCountryChange}
        />

        <!-- State/Province — conditionally shown -->
        {#if countryState.showState}
          <FormCombobox
            {form}
            field="state"
            {...STATE}
            options={countryState.stateOptions}
          />
        {/if}

        <!-- Street address with expandable extra lines -->
        <FormInput
          {form}
          field="street1"
          {...STREET1}
        >
          {#snippet labelRight()}
            <button
              type="button"
              onclick={addressLines.toggleExtraLines}
              class="text-muted-foreground hover:text-foreground text-sm"
            >
              {addressLines.showExtraLines
                ? m.webclient_design_address_fewer()
                : m.webclient_design_address_more()}
            </button>
          {/snippet}
        </FormInput>
        {#if addressLines.showExtraLines}
          <div class="grid gap-4 sm:grid-cols-2">
            <FormInput
              {form}
              field="street2"
              {...STREET2}
            />
            <FormInput
              {form}
              field="street3"
              {...STREET3}
            />
          </div>
        {/if}

        <!-- City + Postal Code row -->
        <div class="grid gap-4 sm:grid-cols-2">
          <FormInput
            {form}
            field="city"
            {...CITY}
            oninput={maskDisplayName}
          />
          <FormInput
            {form}
            field="postalCode"
            {...POSTAL_CODE}
          />
        </div>

        <!-- Actions -->
        <div class="flex justify-end gap-2 pt-2">
          <Button
            variant="outline"
            type="reset">{m.common_ui_reset()}</Button
          >
          <Button type="submit">{m.common_ui_submit()}</Button>
        </div>
      </form>
    </Card.Content>
  </Card.Root>

  <!-- Data source indicator -->
  <p class="text-muted-foreground mt-4 text-center text-xs">
    {m.webclient_design_countries_loaded({ count: String(countries.length) })}
  </p>
</div>
