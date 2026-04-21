<script lang="ts">
  import InlineSwitch from "$lib/client/components/forms/inline-switch.svelte";
  import * as Alert from "$lib/client/components/ui/alert/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import AccountVerificationModal from "$lib/client/components/account/account-verification-modal.svelte";
  import RemovePhoneDialog from "$lib/client/components/account/remove-phone-dialog.svelte";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";
  import { page } from "$app/stores";
  import { formatPhoneForDisplay } from "$lib/shared/utils/phone-format.js";
  import CheckCircleIcon from "@lucide/svelte/icons/circle-check";
  import XIcon from "@lucide/svelte/icons/x";
  import AlertTriangleIcon from "@lucide/svelte/icons/triangle-alert";
  import InfoIcon from "@lucide/svelte/icons/info";
  import {
    getMyNotificationPreferences,
    setMyNotificationPreferences,
  } from "$lib/client/rest/notification-preferences-client.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { onMount } from "svelte";

  let { data } = $props();

  // --- User data from layout server load ---
  const user = $derived(
    $page.data.user as {
      id: string;
      email?: string;
      emailVerified?: boolean;
      phone?: string;
      phoneVerified?: boolean;
    } | null,
  );

  // --- Modal state ---
  let emailModalOpen = $state(false);
  let phoneModalOpen = $state(false);
  let removePhoneOpen = $state(false);

  // Mirrors profile page pattern — skeleton until SSR-derived user data is in
  // hand. Avoids checking `!user` (which is never null with SSR per CLAUDE.md).
  let loaded = $state(false);
  $effect(() => {
    if (user) loaded = true;
  });

  // --- Notification preferences (real — gateway → Comms) ---
  // Channel prefs are opt-OUT: when no row exists, both channels deliver by
  // default (matches backend `resolveChannels` rule — `prefs?.emailEnabled ?? true`).
  // Initialize to true so the toggles display correctly while the GET completes.
  let emailNotifications = $state(true);
  let smsNotifications = $state(true);
  let prefsLoaded = $state(false);

  onMount(() => {
    void (async () => {
      const result = await getMyNotificationPreferences();
      if (result.success && result.data) {
        emailNotifications = result.data.emailEnabled;
        smsNotifications = result.data.smsEnabled;
      }
      // Flip even on failure — defaults already applied; better than skeletoning
      // forever if the GET errors.
      prefsLoaded = true;
    })();
  });

  async function saveEmailPref(value: boolean): Promise<void> {
    const result = await setMyNotificationPreferences({ emailEnabled: value });
    if (!result.success) {
      throw new Error(translateMessage(result.messages?.[0], undefined, m.common_errors_unknown()));
    }
    toast.success(m.common_ui_changes_saved());
  }

  async function saveSmsPref(value: boolean): Promise<void> {
    const result = await setMyNotificationPreferences({ smsEnabled: value });
    if (!result.success) {
      throw new Error(translateMessage(result.messages?.[0], undefined, m.common_errors_unknown()));
    }
    toast.success(m.common_ui_changes_saved());
  }

  // --- Phone display ---
  const phoneDisplay = $derived(() => (user?.phone ? formatPhoneForDisplay(user.phone) : null));
</script>

<svelte:head>
  <title
    >{m.account_page_title()} / {m.account_email_phone_title()} — {m.webclient_nav_brand()}</title
  >
  <meta
    name="description"
    content={m.account_email_phone_description()}
  />
</svelte:head>

<div class="space-y-10">
  <header>
    <h1 class="text-xl font-semibold">{m.account_email_phone_title()}</h1>
    <p class="text-muted-foreground mt-1 text-sm">{m.account_email_phone_description()}</p>
  </header>

  <section class="space-y-3">
    <div>
      <h2 class="text-base font-semibold">{m.account_email_address_title()}</h2>
      <p class="text-muted-foreground mt-1 text-sm">{m.account_email_address_description()}</p>
    </div>
    {#if loaded}
      <div class="flex items-center justify-between gap-4">
        <div class="flex min-w-0 flex-1 items-center gap-2">
          <span class="truncate text-sm">{user?.email ?? ""}</span>
          {#if user?.emailVerified}
            <span class="text-muted-foreground inline-flex items-center gap-1 text-xs">
              <CheckCircleIcon class="size-3.5 text-green-500" />
              {m.account_verified_label()}
            </span>
          {/if}
        </div>
        <Button
          variant="outline"
          size="sm"
          onclick={() => (emailModalOpen = true)}>{m.account_email_change_button()}</Button
        >
      </div>
    {:else}
      <div class="flex items-center justify-between gap-4">
        <div class="flex min-w-0 flex-1 items-center gap-2">
          <Skeleton class="h-5 w-48" />
          <Skeleton class="h-4 w-16" />
        </div>
        <Skeleton class="h-8 w-20 rounded-md" />
      </div>
    {/if}
  </section>

  <section class="space-y-3">
    <div>
      <h2 class="text-base font-semibold">{m.account_phone_title()}</h2>
      <p class="text-muted-foreground mt-1 text-sm">{m.account_phone_description()}</p>
    </div>
    {#if loaded}
      <div class="flex items-center justify-between gap-4">
        <div class="flex min-w-0 flex-1 items-center gap-2">
          {#if user?.phone}
            <span class="truncate text-sm">{phoneDisplay()}</span>
            {#if user.phoneVerified}
              <span class="text-muted-foreground inline-flex items-center gap-1 text-xs">
                <CheckCircleIcon class="size-3.5 text-green-500" />
                {m.account_verified_label()}
              </span>
            {/if}
          {:else}
            <span class="text-muted-foreground text-sm">{m.account_phone_not_added()}</span>
          {/if}
        </div>
        <div class="flex items-center gap-2">
          {#if user?.phone}
            <Button
              variant="ghost"
              size="icon"
              class="size-8"
              onclick={() => (removePhoneOpen = true)}
              title={m.account_phone_remove_button()}
            >
              <XIcon class="size-4" />
              <span class="sr-only">{m.account_phone_remove_button()}</span>
            </Button>
          {/if}
          <Button
            variant="outline"
            size="sm"
            onclick={() => (phoneModalOpen = true)}
          >
            {user?.phone ? m.account_phone_change_button() : m.account_phone_add_button()}
          </Button>
        </div>
      </div>
    {:else}
      <div class="flex items-center justify-between gap-4">
        <div class="flex min-w-0 flex-1 items-center gap-2">
          <Skeleton class="h-5 w-40" />
          <Skeleton class="h-4 w-16" />
        </div>
        <Skeleton class="h-8 w-20 rounded-md" />
      </div>
    {/if}

    <Alert.Root variant="warning">
      <AlertTriangleIcon />
      <Alert.Title>{m.account_phone_dev_alert_title()}</Alert.Title>
      <Alert.Description>{m.account_phone_dev_alert_body()}</Alert.Description>
    </Alert.Root>
  </section>

  <section class="space-y-3">
    <div>
      <h2 class="text-base font-semibold">{m.account_notifications_title()}</h2>
      <p class="text-muted-foreground mt-1 text-sm">{m.account_notifications_description()}</p>
    </div>
    <Alert.Root variant="info">
      <InfoIcon />
      <Alert.Title>{m.account_notifications_alert_title()}</Alert.Title>
      <Alert.Description>{m.account_notifications_alert_body()}</Alert.Description>
    </Alert.Root>

    {#if prefsLoaded}
      <InlineSwitch
        bind:value={emailNotifications}
        label={m.account_email_notifications()}
        description={m.account_email_notifications_description()}
        onSave={saveEmailPref}
      />
      <InlineSwitch
        bind:value={smsNotifications}
        label={m.account_sms_notifications()}
        description={m.account_sms_notifications_description()}
        onSave={saveSmsPref}
      />
    {:else}
      <div class="flex items-center justify-between gap-3">
        <div class="flex flex-col gap-1">
          <Skeleton class="h-4 w-32" />
          <Skeleton class="h-3 w-56" />
        </div>
        <Skeleton class="h-6 w-11 rounded-full" />
      </div>
      <div class="flex items-center justify-between gap-3">
        <div class="flex flex-col gap-1">
          <Skeleton class="h-4 w-32" />
          <Skeleton class="h-3 w-56" />
        </div>
        <Skeleton class="h-6 w-11 rounded-full" />
      </div>
    {/if}
  </section>
</div>

<AccountVerificationModal
  bind:open={emailModalOpen}
  type="email"
  countries={data.countries}
/>
<AccountVerificationModal
  bind:open={phoneModalOpen}
  type="phone"
  countries={data.countries}
/>
<RemovePhoneDialog bind:open={removePhoneOpen} />
