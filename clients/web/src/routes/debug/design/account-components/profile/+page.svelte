<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineEditFieldGroup from "$lib/client/components/forms/inline-edit-field-group.svelte";
  import InlineDropdown from "$lib/client/components/forms/inline-dropdown.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import { page } from "$app/stores";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";

  let username = $state("JohnDoe42");
  let locale = $state("en-US");
  let timezone = $state("America/New_York");

  let nameFields = $state([
    {
      key: "firstName",
      label: "First Name",
      value: "John",
      placeholder: "First name",
      maxLength: 255,
    },
    {
      key: "lastName",
      label: "Last Name",
      value: "Doe",
      placeholder: "Last name",
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
    toast.success(`Saved: "${value}"`);
  }

  async function mockSaveGroup(values: Record<string, string>) {
    await new Promise((r) => setTimeout(r, 1000));
    toast.success(`Saved: ${JSON.stringify(values)}`);
  }

  function validateUsername(value: string) {
    if (!value) return "Username is required.";
    if (!/^[a-zA-Z0-9]+$/.test(value)) return "Letters and numbers only.";
    if (value.length < 3) return "Must be at least 3 characters.";
    if (value.length > 32) return "Must be 32 characters or fewer.";
    return undefined;
  }

  async function checkUsernameAvailable(value: string) {
    await new Promise((r) => setTimeout(r, 500));
    if (value.toLowerCase() === "admin") return "Username is already taken.";
    return undefined;
  }

  function validateNameGroup(values: Record<string, string>) {
    const errors: Record<string, string> = {};
    if (!values.firstName?.trim()) errors.firstName = "First name is required.";
    if (!values.lastName?.trim()) errors.lastName = "Last name is required.";
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

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Profile</h2>
    <p class="text-muted-foreground text-sm">Manage your personal information.</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Your Information</Card.Title>
      <Card.Description>Your name and unique identifier.</Card.Description>
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
        label="Username"
        placeholder="Enter username"
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
      <Card.Title class="text-base">Language & Time</Card.Title>
      <Card.Description>Your preferred language and timezone.</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-5">
      <InlineDropdown
        bind:value={locale}
        label="Language"
        options={localeOptions}
        onSave={mockSave}
        onDirtyChange={(d) => (dirtyFields.locale = d)}
        bind:this={localeRef}
      />
      <InlineDropdown
        bind:value={timezone}
        label="Timezone"
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
