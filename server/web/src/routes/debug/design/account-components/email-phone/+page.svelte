<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineSwitch from "$lib/client/components/forms/inline-switch.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";

  let email = $state("john@example.com");
  let emailNotifications = $state(true);
  let smsNotifications = $state(false);

  let emailFieldRef: InlineEditField | undefined = $state();

  let dirtyFields = $state({
    email: false,
  });
  const anyDirty = $derived(Object.values(dirtyFields).some(Boolean));

  async function mockSaveFail() {
    await new Promise((r) => setTimeout(r, 800));
    throw new Error(m.webclient_debug_account_components_email_change_unsupported());
  }

  async function mockSaveBool(value: boolean) {
    await new Promise((r) => setTimeout(r, 800));
    toast.success(
      value
        ? m.webclient_debug_account_components_pref_updated_enabled()
        : m.webclient_debug_account_components_pref_updated_disabled(),
    );
  }

  async function saveAll() {
    if (emailFieldRef?.getDirty()) {
      const ok = await emailFieldRef.saveIfDirty();
      if (!ok) return;
    }
  }

  function discardAll() {
    emailFieldRef?.revert();
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_account_components_email_phone_page_title()}</title>
  <meta
    name="description"
    content={m.webclient_debug_account_components_email_phone_page_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.webclient_app_account_email_phone_title()}</h2>
    <p class="text-muted-foreground text-sm">
      {m.webclient_app_account_email_phone_description()}
    </p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_email_address_title()}</Card.Title>
      <Card.Description>
        {m.webclient_debug_account_components_email_change_unsupported()}
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <InlineEditField
        bind:value={email}
        label={m.webclient_forms_email_label()}
        placeholder={m.webclient_forms_email_placeholder()}
        onSave={mockSaveFail}
        onDirtyChange={(d) => (dirtyFields.email = d)}
        bind:this={emailFieldRef}
      />
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_notifications_title()}</Card.Title>
      <Card.Description>{m.webclient_app_account_notifications_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <InlineSwitch
        bind:value={emailNotifications}
        label={m.webclient_app_account_email_notifications()}
        description={m.webclient_app_account_email_notifications_description()}
        onSave={mockSaveBool}
      />
      <InlineSwitch
        bind:value={smsNotifications}
        label={m.webclient_app_account_sms_notifications()}
        description={m.webclient_app_account_sms_notifications_description()}
        onSave={mockSaveBool}
      />
    </Card.Content>
  </Card.Root>
</div>

<UnsavedChangesBar
  visible={anyDirty}
  onSave={saveAll}
  onDiscard={discardAll}
/>
