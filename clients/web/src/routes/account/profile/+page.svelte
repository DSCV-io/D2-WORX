<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineEditFieldGroup from "$lib/client/components/forms/inline-edit-field-group.svelte";
  import InlineDropdown from "$lib/client/components/forms/inline-dropdown.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import { page } from "$app/stores";
  import { getLocale } from "$lib/paraglide/runtime";
  import * as m from "$lib/paraglide/messages.js";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";

  // --- User data from layout server load ---
  const user = $derived(
    $page.data.user as {
      id: string;
      name?: string;
      email?: string;
      username?: string;
      displayUsername?: string;
      image?: string;
    } | null,
  );

  // Split "firstName lastName" into separate fields
  const nameParts = $derived(() => {
    if (!user?.name) return { first: "", last: "" };
    const parts = user.name.trim().split(/\s+/);
    if (parts.length >= 2) return { first: parts[0], last: parts.slice(1).join(" ") };
    return { first: parts[0] ?? "", last: "" };
  });

  let username = $state("");
  let locale = $state(getLocale());
  let timezone = $state(Intl.DateTimeFormat().resolvedOptions().timeZone);

  let nameFields = $state([
    {
      key: "firstName",
      label: m.account_profile_first_name(),
      value: "",
      placeholder: m.account_profile_first_name(),
      maxLength: 255,
    },
    {
      key: "lastName",
      label: m.account_profile_last_name(),
      value: "",
      placeholder: m.account_profile_last_name(),
      maxLength: 255,
    },
  ]);

  // Sync initial values from user data once available
  $effect(() => {
    if (user) {
      const parts = nameParts();
      nameFields[0].value = parts.first;
      nameFields[1].value = parts.last;
      username = user.displayUsername ?? user.username ?? "";
    }
  });

  let nameFieldGroupRef: InlineEditFieldGroup | undefined = $state();
  let usernameFieldRef: InlineEditField | undefined = $state();
  let localeRef: InlineDropdown | undefined = $state();
  let timezoneRef: InlineDropdown | undefined = $state();

  // Locale options from root layout (Geo ref data)
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

  // --- Real save handlers ---

  async function saveName(values: Record<string, string>) {
    const response = await fetch("/api/account/name", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ firstName: values.firstName, lastName: values.lastName }),
    });
    const result = await response.json();
    if (!response.ok) {
      const msg =
        result.messages?.[0] ?? result.inputErrors?.firstName ?? result.inputErrors?.lastName;
      throw new Error(msg ?? m.common_errors_unknown());
    }
    toast.success(m.common_ui_save());
  }

  async function saveUsername(value: string) {
    const response = await fetch("/api/account/username", {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: value }),
    });
    const result = await response.json();
    if (!response.ok) {
      const msg = result.messages?.[0] ?? result.inputErrors?.username;
      throw new Error(msg ?? m.common_errors_unknown());
    }
    toast.success(m.common_ui_save());
  }

  // TODO: Wire to backend when locale/timezone update handlers are created
  async function mockSaveLocale(value: string) {
    await new Promise((r) => setTimeout(r, 500));
    toast.success(m.common_ui_save());
  }

  async function mockSaveTimezone(value: string) {
    await new Promise((r) => setTimeout(r, 500));
    toast.success(m.common_ui_save());
  }

  // --- Validation ---

  function validateUsername(value: string) {
    if (!value) return m.account_profile_username_required();
    if (!/^[a-zA-Z0-9]+$/.test(value)) return m.account_profile_username_alpha();
    if (value.length < 3) return m.account_profile_username_min();
    if (value.length > 32) return m.account_profile_username_max();
    return undefined;
  }

  function validateNameGroup(values: Record<string, string>) {
    const errors: Record<string, string> = {};
    if (!values.firstName?.trim()) errors.firstName = m.account_profile_first_name_required();
    if (!values.lastName?.trim()) errors.lastName = m.account_profile_last_name_required();
    return Object.keys(errors).length > 0 ? errors : undefined;
  }

  // --- Save all / discard all ---

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
  <title>{m.account_profile_title()}</title>
  <meta
    name="description"
    content={m.account_profile_description()}
  />
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.account_profile_title()}</h2>
    <p class="text-muted-foreground text-sm">{m.account_profile_description()}</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_profile_your_info_title()}</Card.Title>
      <Card.Description>{m.account_profile_your_info_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-5">
      <InlineEditFieldGroup
        bind:fields={nameFields}
        validate={validateNameGroup}
        onSave={saveName}
        onDirtyChange={(d) => (dirtyFields.name = d)}
        bind:this={nameFieldGroupRef}
      />
      <InlineEditField
        bind:value={username}
        label={m.account_profile_username()}
        placeholder={m.account_profile_username_placeholder()}
        maxLength={32}
        validate={validateUsername}
        onSave={saveUsername}
        onDirtyChange={(d) => (dirtyFields.username = d)}
        bind:this={usernameFieldRef}
      />
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_profile_language_time_title()}</Card.Title>
      <Card.Description>{m.account_profile_language_time_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-5">
      <InlineDropdown
        bind:value={locale}
        label={m.account_profile_language()}
        options={localeOptions}
        onSave={mockSaveLocale}
        onDirtyChange={(d) => (dirtyFields.locale = d)}
        bind:this={localeRef}
      />
      <InlineDropdown
        bind:value={timezone}
        label={m.account_profile_timezone()}
        options={timezoneOptions}
        onSave={mockSaveTimezone}
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
