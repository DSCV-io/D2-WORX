<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineSwitch from "$lib/client/components/forms/inline-switch.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";

  let email = $state("john@example.com");
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
    toast.success(`Preference updated: ${value ? "enabled" : "disabled"}`);
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

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Email & Phone</h2>
    <p class="text-muted-foreground text-sm">
      Manage your contact details and notification preferences.
    </p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Email Address</Card.Title>
      <Card.Description>Your email address. Changes are not yet supported.</Card.Description>
    </Card.Header>
    <Card.Content>
      <InlineEditField
        bind:value={email}
        label="Email"
        placeholder="Email address"
        onSave={mockSaveFail}
        onDirtyChange={(d) => (dirtyFields.email = d)}
        bind:this={emailFieldRef}
      />
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Notification Preferences</Card.Title>
      <Card.Description>Control how you receive notifications.</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-4">
      <InlineSwitch
        bind:value={emailNotifications}
        label="Email notifications"
        description="Receive email notifications for important updates."
        onSave={mockSaveBool}
        onDirtyChange={(d) => (dirtyFields.emailNotif = d)}
        bind:this={emailNotifRef}
      />
      <InlineSwitch
        bind:value={smsNotifications}
        label="SMS notifications"
        description="Receive text message alerts for urgent items."
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
