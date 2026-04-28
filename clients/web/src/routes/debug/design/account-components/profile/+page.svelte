<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineEditFieldGroup from "$lib/client/components/forms/inline-edit-field-group.svelte";
  import InlineDropdown from "$lib/client/components/forms/inline-dropdown.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import { page } from "$app/stores";
  import * as m from "$lib/paraglide/messages.js";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";

  let username = $state("JohnDoe42");
  let locale = $state("en-US");
  let timezone = $state("America/New_York");

  let nameFields = $state([
    {
      key: "firstName",
      label: m.webclient_app_account_profile_first_name(),
      value: "John",
      placeholder: m.webclient_app_account_profile_first_name(),
      maxLength: 255,
    },
    {
      key: "lastName",
      label: m.webclient_app_account_profile_last_name(),
      value: "Doe",
      placeholder: m.webclient_app_account_profile_last_name(),
      maxLength: 255,
    },
  ]);

  let nameFieldGroupRef: InlineEditFieldGroup | undefined = $state();
  let usernameFieldRef: InlineEditField | undefined = $state();
  let localeRef: InlineDropdown | undefined = $state();
  let timezoneRef: InlineDropdown | undefined = $state();

  // Locale options resolved from Geo ref data via root layout
  const rawLocales: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );
  const localeOptions = $derived(
    rawLocales.map((l) => ({ value: l.code, label: l.endonym, image: l.flag })),
  );

  const timezoneOptions = [
    { value: "America/New_York", label: "Eastern (ET)" },
    { value: "America/Chicago", label: "Central (CT)" },
    { value: "America/Denver", label: "Mountain (MT)" },
    { value: "America/Los_Angeles", label: "Pacific (PT)" },
    { value: "Europe/London", label: "London (GMT)" },
    { value: "Europe/Berlin", label: "Berlin (CET)" },
    { value: "Europe/Paris", label: "Paris (CET)" },
    { value: "Asia/Tokyo", label: "Tokyo (JST)" },
  ];

  let dirtyFields = $state({
    name: false,
    username: false,
    locale: false,
    timezone: false,
  });
  const anyDirty = $derived(Object.values(dirtyFields).some(Boolean));

  async function mockSave(value: string) {
    await new Promise((r) => setTimeout(r, 1000));
    toast.success(m.webclient_debug_account_components_saved_value({ value }));
  }

  async function mockSaveGroup(values: Record<string, string>) {
    await new Promise((r) => setTimeout(r, 1000));
    toast.success(
      m.webclient_debug_account_components_saved_values({ values: JSON.stringify(values) }),
    );
  }

  function validateUsername(value: string) {
    if (!value) return m.webclient_app_account_profile_username_required();
    if (!/^[a-zA-Z0-9]+$/.test(value)) return m.webclient_app_account_profile_username_alpha();
    if (value.length < 3) return m.webclient_app_account_profile_username_min();
    if (value.length > 32) return m.webclient_app_account_profile_username_max();
    return undefined;
  }

  async function checkUsernameAvailable(value: string) {
    await new Promise((r) => setTimeout(r, 500));
    if (value.toLowerCase() === "admin") return m.webclient_app_account_profile_username_taken();
    return undefined;
  }

  function validateNameGroup(values: Record<string, string>) {
    const errors: Record<string, string> = {};
    if (!values.firstName?.trim())
      errors.firstName = m.webclient_app_account_profile_first_name_required();
    if (!values.lastName?.trim())
      errors.lastName = m.webclient_app_account_profile_last_name_required();
    return Object.keys(errors).length > 0 ? errors : undefined;
  }

  async function saveAll() {
    if (nameFieldGroupRef?.getDirty()) {
      const ok = await nameFieldGroupRef.saveIfDirty();
      if (!ok) return;
    }
    if (usernameFieldRef?.getDirty()) {
      const ok = await usernameFieldRef.saveIfDirty();
      if (!ok) return;
    }
    if (localeRef?.getDirty()) {
      const ok = await localeRef.saveIfDirty();
      if (!ok) return;
    }
    if (timezoneRef?.getDirty()) {
      const ok = await timezoneRef.saveIfDirty();
      if (!ok) return;
    }
  }

  function discardAll() {
    nameFieldGroupRef?.revert();
    usernameFieldRef?.revert();
    localeRef?.revert();
    timezoneRef?.revert();
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_account_components_profile_page_title()}</title>
  <meta
    name="description"
    content={m.webclient_debug_account_components_profile_page_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.webclient_app_account_profile_title()}</h2>
    <p class="text-muted-foreground text-sm">{m.webclient_app_account_profile_description()}</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_profile_your_info_title()}</Card.Title>
      <Card.Description>
        {m.webclient_app_account_profile_your_info_description()}
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-5">
      <InlineEditFieldGroup
        bind:fields={nameFields}
        validate={validateNameGroup}
        onSave={mockSaveGroup}
        onDirtyChange={(d) => (dirtyFields.name = d)}
        bind:this={nameFieldGroupRef}
      />
      <InlineEditField
        bind:value={username}
        label={m.webclient_app_account_profile_username()}
        placeholder={m.webclient_app_account_profile_username_placeholder()}
        maxLength={32}
        validate={validateUsername}
        asyncValidate={checkUsernameAvailable}
        onSave={mockSave}
        onDirtyChange={(d) => (dirtyFields.username = d)}
        bind:this={usernameFieldRef}
      />
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">
        {m.webclient_app_account_profile_language_time_title()}
      </Card.Title>
      <Card.Description>
        {m.webclient_app_account_profile_language_time_description()}
      </Card.Description>
    </Card.Header>
    <Card.Content class="space-y-5">
      <InlineDropdown
        bind:value={locale}
        label={m.webclient_app_account_profile_language()}
        options={localeOptions}
        onSave={mockSave}
        onDirtyChange={(d) => (dirtyFields.locale = d)}
        bind:this={localeRef}
      />
      <InlineDropdown
        bind:value={timezone}
        label={m.webclient_app_account_profile_timezone()}
        options={timezoneOptions}
        onSave={mockSave}
        onDirtyChange={(d) => (dirtyFields.timezone = d)}
        bind:this={timezoneRef}
      />
    </Card.Content>
  </Card.Root>
</div>

<UnsavedChangesBar
  visible={anyDirty}
  onSave={saveAll}
  onDiscard={discardAll}
/>
