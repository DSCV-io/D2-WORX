<script lang="ts">
  import InlineEditField from "$lib/client/components/forms/inline-edit-field.svelte";
  import InlineEditFieldGroup from "$lib/client/components/forms/inline-edit-field-group.svelte";
  import UnsavedChangesBar from "$lib/client/components/ui/unsaved-changes-bar.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";

  let username = $state("JohnDoe42");
  let email = $state("john@example.com");

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
  let emailFieldRef: InlineEditField | undefined = $state();

  // Track dirty state from each field
  let dirtyFields = $state({ name: false, username: false, email: false });
  const anyDirty = $derived(Object.values(dirtyFields).some(Boolean));

  // --- Mock async callbacks ---
  async function mockSave(value: string) {
    await new Promise((r) => setTimeout(r, 1000));
    toast.success(`Saved: "${value}"`);
  }

  async function mockSaveGroup(values: Record<string, string>) {
    await new Promise((r) => setTimeout(r, 1000));
    toast.success(`Saved: ${JSON.stringify(values)}`);
  }

  async function mockSaveFail() {
    await new Promise((r) => setTimeout(r, 800));
    throw new Error("Email change is not yet supported.");
  }

  function validateUsername(value: string) {
    if (!value) return "Username is required.";
    if (!/^[a-zA-Z0-9]+$/.test(value)) return "Letters and numbers only.";
    if (value.length < 3) return "Must be at least 3 characters.";
    if (value.length > 32) return "Must be 32 characters or fewer.";
    return undefined;
  }

  async function checkUsernameAvailable(value: string) {
    // Simulate async availability check
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
    // Save dirty fields sequentially
    if (nameFieldGroupRef?.getDirty()) {
      const ok = await nameFieldGroupRef.saveIfDirty();
      if (!ok) return;
    }
    if (usernameFieldRef?.getDirty()) {
      const ok = await usernameFieldRef.saveIfDirty();
      if (!ok) return;
    }
    if (emailFieldRef?.getDirty()) {
      const ok = await emailFieldRef.saveIfDirty();
      if (!ok) return;
    }
  }

  function discardAll() {
    nameFieldGroupRef?.revert();
    usernameFieldRef?.revert();
    emailFieldRef?.revert();
  }
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Profile</h2>
    <p class="text-muted-foreground text-sm">Manage your personal information.</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Name</Card.Title>
      <Card.Description>Your first and last name as displayed to others.</Card.Description>
    </Card.Header>
    <Card.Content>
      <InlineEditFieldGroup
        bind:fields={nameFields}
        validate={validateNameGroup}
        onSave={mockSaveGroup}
        onDirtyChange={(d) => (dirtyFields.name = d)}
        bind:this={nameFieldGroupRef}
      />
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Username</Card.Title>
      <Card.Description>
        Your unique identifier. Letters and numbers only. Try "admin" to see async validation.
      </Card.Description>
    </Card.Header>
    <Card.Content>
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
      <Card.Title class="text-base">Email</Card.Title>
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
</div>

<UnsavedChangesBar
  visible={anyDirty}
  onSave={saveAll}
  onDiscard={discardAll}
/>
