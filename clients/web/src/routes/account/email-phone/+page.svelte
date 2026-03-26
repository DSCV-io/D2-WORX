<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineSwitch from "$lib/client/components/forms/inline-switch.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";

  let email = $state("");
  let emailNotifications = $state(true);
  let smsNotifications = $state(false);

  let emailFieldRef: InlineEditField | undefined = $state();
  let emailNotifRef: InlineSwitch | undefined = $state();
  let smsNotifRef: InlineSwitch | undefined = $state();

  let dirtyFields = $state({
    email: false,
    emailNotif: false,
    smsNotif: false,
  });
  const anyDirty = $derived(Object.values(dirtyFields).some(Boolean));

  async function mockSaveFail() {
    await new Promise((r) => setTimeout(r, 800));
    throw new Error("Email change is not yet supported.");
  }

  async function mockSaveBool(value: boolean) {
    await new Promise((r) => setTimeout(r, 800));
    toast.success(m.common_ui_changes_saved());
  }

  async function saveAll() {
    if (emailFieldRef?.getDirty()) {
      const ok = await emailFieldRef.saveIfDirty();
      if (!ok) return;
    }
    if (emailNotifRef?.getDirty()) {
      const ok = await emailNotifRef.saveIfDirty();
      if (!ok) return;
    }
    if (smsNotifRef?.getDirty()) {
      const ok = await smsNotifRef.saveIfDirty();
      if (!ok) return;
    }
  }

  function discardAll() {
    emailFieldRef?.revert();
    emailNotifRef?.revert();
    smsNotifRef?.revert();
  }
</script>

<svelte:head>
  <title>{m.account_email_phone_title()}</title>
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.account_email_phone_title()}</h2>
    <p class="text-muted-foreground text-sm">{m.account_email_phone_description()}</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_email_address_title()}</Card.Title>
      <Card.Description>{m.account_email_address_description()}</Card.Description>
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
      <Card.Title class="text-base">{m.account_notifications_title()}</Card.Title>
      <Card.Description>{m.account_notifications_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <InlineSwitch
        bind:value={emailNotifications}
        label={m.account_email_notifications()}
        description={m.account_email_notifications_description()}
        onSave={mockSaveBool}
        onDirtyChange={(d) => (dirtyFields.emailNotif = d)}
        bind:this={emailNotifRef}
      />
      <InlineSwitch
        bind:value={smsNotifications}
        label={m.account_sms_notifications()}
        description={m.account_sms_notifications_description()}
        onSave={mockSaveBool}
        onDirtyChange={(d) => (dirtyFields.smsNotif = d)}
        bind:this={smsNotifRef}
      />
    </Card.Content>
  </Card.Root>
</div>

<UnsavedChangesBar
  visible={anyDirty}
  onSave={saveAll}
  onDiscard={discardAll}
/>
