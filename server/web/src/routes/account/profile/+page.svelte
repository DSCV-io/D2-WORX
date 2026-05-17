<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import AvatarUploader from "$lib/client/components/account/avatar-uploader.svelte";
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineEditFieldGroup from "$lib/client/components/forms/inline-edit-field-group.svelte";
  import InlineDropdown from "$lib/client/components/forms/inline-dropdown.svelte";
  import InlineCombobox from "$lib/client/components/forms/inline-combobox.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import ConfirmationDialog from "$lib/client/components/ui/confirmation-dialog.svelte";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { toast } from "svelte-sonner";
  import {
    updateName as updateNameApi,
    updateUsername as updateUsernameApi,
  } from "$lib/client/rest/account-client.js";
  import { page } from "$app/stores";
  import { getLocale } from "$lib/paraglide/runtime";
  import { changeLocale } from "$lib/client/utils/change-locale.js";
  import { changeTimezone } from "$lib/client/utils/change-timezone.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { SaveCanceledError } from "$lib/shared/forms/save-canceled-error.js";
  import * as m from "$lib/paraglide/messages.js";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";
  import type { TimezoneOption } from "$lib/shared/forms/timezone-options.js";

  // --- User data from layout server load ---
  const user = $derived(
    $page.data.user as {
      id: string;
      name?: string;
      email?: string;
      username?: string;
      displayUsername?: string;
      image?: string;
      locale?: string;
      timezone?: string;
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
      label: m.webclient_app_account_profile_first_name(),
      value: "",
      placeholder: m.webclient_app_account_profile_first_name(),
      maxLength: 255,
    },
    {
      key: "lastName",
      label: m.webclient_app_account_profile_last_name(),
      value: "",
      placeholder: m.webclient_app_account_profile_last_name(),
      maxLength: 255,
    },
  ]);

  // Track whether initial data has been synced into the form fields.
  // Skeletons show until this is true (avoids flash of empty placeholders).
  let loaded = $state(false);

  // Sync initial values from user data once available
  $effect(() => {
    if (user) {
      const parts = nameParts();
      nameFields[0].value = parts.first;
      nameFields[1].value = parts.last;
      username = user.displayUsername ?? user.username ?? "";
      locale = (user.locale as ReturnType<typeof getLocale> | undefined) ?? getLocale();
      timezone = user.timezone ?? Intl.DateTimeFormat().resolvedOptions().timeZone;
      loaded = true;
    }
  });

  let nameFieldGroupRef: InlineEditFieldGroup | undefined = $state();
  let usernameFieldRef: InlineEditField | undefined = $state();
  let localeRef: InlineDropdown | undefined = $state();
  let timezoneRef: InlineCombobox | undefined = $state();

  // Locale options from root layout (Geo ref data)
  const rawLocales: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );
  const localeOptions = $derived(
    rawLocales.map((l) => ({ value: l.code, label: l.endonym, image: l.flag })),
  );

  const rawTimezones: TimezoneOption[] = $derived(
    ($page.data as { timezoneOptions?: TimezoneOption[] }).timezoneOptions ?? [],
  );
  const timezoneOptions = $derived(rawTimezones.map((t) => ({ value: t.value, label: t.label })));

  let dirtyFields = $state({
    name: false,
    username: false,
    locale: false,
    timezone: false,
  });
  const anyDirty = $derived(Object.values(dirtyFields).some(Boolean));

  // --- Real save handlers ---

  async function saveName(values: Record<string, string>) {
    const result = await updateNameApi(values.firstName, values.lastName);
    if (!result.success) {
      // Prefer the first top-level message; fall back to the first
      // input-error's first TKMessage (object shape per
      // contracts/input-error spec — NOT tuple indexing).
      const message =
        result.messages?.[0] ?? result.inputErrors?.[0]?.errors?.[0];
      throw new Error(translateMessage(message, undefined, m.common_errors_UNKNOWN()));
    }
    toast.success(m.common_ui_changes_saved());
  }

  async function saveUsername(value: string) {
    const result = await updateUsernameApi(value);
    if (!result.success) {
      // Prefer the first top-level message; fall back to the first
      // input-error's first TKMessage (object shape per
      // contracts/input-error spec — NOT tuple indexing).
      const message =
        result.messages?.[0] ?? result.inputErrors?.[0]?.errors?.[0];
      throw new Error(translateMessage(message, undefined, m.common_errors_UNKNOWN()));
    }
    toast.success(m.common_ui_changes_saved());
  }

  let pendingLocale = $state("");
  let localeConfirmOpen = $state(false);
  // Resolver wired into the confirmation dialog: confirm → resolve(true),
  // cancel → resolve(false). Lets `saveLocale` await the user's decision so
  // the InlineDropdown's saveState stays in sync with what actually happened.
  let localeConfirmResolve: ((confirmed: boolean) => void) | null = null;

  async function saveLocale(value: string) {
    pendingLocale = value;
    const confirmed = await new Promise<boolean>((resolve) => {
      localeConfirmResolve = resolve;
      localeConfirmOpen = true;
    });
    if (!confirmed) {
      // User canceled — let the dropdown stay dirty (save/revert reappear)
      // by signaling cancellation via the sentinel.
      throw new SaveCanceledError();
    }
    await changeLocale(value, true);
  }

  async function confirmLocaleChange() {
    localeConfirmResolve?.(true);
    localeConfirmResolve = null;
  }

  function cancelLocaleChange() {
    localeConfirmResolve?.(false);
    localeConfirmResolve = null;
  }

  async function saveTimezone(value: string) {
    await changeTimezone(value, true);
    toast.success(m.common_ui_changes_saved());
  }

  // --- Validation ---

  function validateUsername(value: string) {
    if (!value) return m.webclient_app_account_profile_username_required();
    if (!/^[a-zA-Z0-9]+$/.test(value)) return m.webclient_app_account_profile_username_alpha();
    if (value.length < 3) return m.webclient_app_account_profile_username_min();
    if (value.length > 32) return m.webclient_app_account_profile_username_max();
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
  <title
    >{m.webclient_app_account_page_title()} / {m.webclient_app_account_profile_title()} — {m.webclient_nav_brand()}</title
  >
  <meta
    name="description"
    content={m.webclient_app_account_profile_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
  <meta
    property="og:title"
    content="{m.webclient_app_account_page_title()} / {m.webclient_app_account_profile_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_app_account_profile_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="space-y-12">
  <header>
    <h1 class="text-2xl font-semibold tracking-tight">{m.webclient_app_account_profile_title()}</h1>
    <p class="text-muted-foreground mt-1 text-sm">
      {m.webclient_app_account_profile_description()}
    </p>
  </header>

  <Separator class="bg-border/50" />

  <section>
    <div>
      <h2 class="text-base font-semibold">{m.webclient_app_account_profile_your_info_title()}</h2>
      <p class="text-muted-foreground mt-0.5 text-sm">
        {m.webclient_app_account_profile_your_info_description()}
      </p>
    </div>
    <div class="mt-5 space-y-5">
      {#if loaded}
        <InlineEditFieldGroup
          bind:fields={nameFields}
          validate={validateNameGroup}
          onSave={saveName}
          onDirtyChange={(d) => (dirtyFields.name = d)}
          bind:this={nameFieldGroupRef}
        />
        <InlineEditField
          bind:value={username}
          label={m.webclient_app_account_profile_username()}
          placeholder={m.webclient_app_account_profile_username_placeholder()}
          maxLength={32}
          validate={validateUsername}
          onSave={saveUsername}
          onDirtyChange={(d) => (dirtyFields.username = d)}
          bind:this={usernameFieldRef}
        />
      {:else}
        <!-- Name fields skeleton -->
        <div class="flex flex-col gap-2 sm:flex-row sm:gap-1.5">
          <div class="flex flex-1 flex-col gap-1.5">
            <Skeleton class="h-5 w-20" />
            <Skeleton class="h-9 w-full rounded-md" />
          </div>
          <div class="flex flex-1 flex-col gap-1.5">
            <Skeleton class="h-5 w-20" />
            <Skeleton class="h-9 w-full rounded-md" />
          </div>
        </div>
        <!-- Username skeleton -->
        <div class="flex flex-col gap-1.5">
          <Skeleton class="h-5 w-24" />
          <Skeleton class="h-9 w-full rounded-md" />
        </div>
      {/if}
    </div>
  </section>

  <Separator class="bg-border/50" />

  <section>
    <div>
      <h2 class="text-base font-semibold">{m.webclient_app_account_profile_avatar_title()}</h2>
      <p class="text-muted-foreground mt-0.5 text-sm">
        {m.webclient_app_account_profile_avatar_description()}
      </p>
    </div>
    <div class="mt-5">
      {#if loaded && user}
        <AvatarUploader
          currentImageFileId={user.image}
          userId={user.id}
          userName={user.name}
        />
      {:else}
        <Skeleton class="size-32 rounded-full" />
      {/if}
    </div>
  </section>

  <Separator class="bg-border/50" />

  <section>
    <div>
      <h2 class="text-base font-semibold">
        {m.webclient_app_account_profile_language_time_title()}
      </h2>
      <p class="text-muted-foreground mt-0.5 text-sm">
        {m.webclient_app_account_profile_language_time_description()}
      </p>
    </div>
    <div class="mt-5 space-y-5">
      {#if loaded}
        <InlineDropdown
          bind:value={locale}
          label={m.webclient_app_account_profile_language()}
          options={localeOptions}
          onSave={saveLocale}
          onDirtyChange={(d) => (dirtyFields.locale = d)}
          bind:this={localeRef}
        />
        <InlineCombobox
          bind:value={timezone}
          label={m.webclient_app_account_profile_timezone()}
          options={timezoneOptions}
          placeholder={m.webclient_app_account_profile_timezone_placeholder()}
          onSave={saveTimezone}
          onDirtyChange={(d) => (dirtyFields.timezone = d)}
          bind:this={timezoneRef}
        />
      {:else}
        <div class="flex flex-col gap-1.5">
          <Skeleton class="h-5 w-20" />
          <Skeleton class="h-9 w-full rounded-md" />
        </div>
        <div class="flex flex-col gap-1.5">
          <Skeleton class="h-5 w-20" />
          <Skeleton class="h-9 w-full rounded-md" />
        </div>
      {/if}
    </div>
  </section>
</div>

<UnsavedChangesBar
  visible={anyDirty}
  onSave={saveAll}
  onDiscard={discardAll}
/>

<ConfirmationDialog
  bind:open={localeConfirmOpen}
  title={m.common_ui_change_language_title()}
  description={m.common_ui_change_language_description()}
  confirmLabel={m.common_ui_change_language_confirm()}
  onConfirm={confirmLocaleChange}
  onCancel={cancelLocaleChange}
/>
