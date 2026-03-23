<script lang="ts">
  import ConfirmationDialog from "$lib/client/components/ui/confirmation-dialog.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";

  let confirmDialogOpen = $state(false);

  async function mockConfirm() {
    await new Promise((r) => setTimeout(r, 1500));
    toast.success("Password changed! (simulated)");
  }
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Security</h2>
    <p class="text-muted-foreground text-sm">Manage your password and security settings.</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Change Password</Card.Title>
      <Card.Description>
        Update your password. All other sessions will be signed out.
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Button onclick={() => (confirmDialogOpen = true)}>Change Password</Button>
    </Card.Content>
  </Card.Root>

  <ConfirmationDialog
    bind:open={confirmDialogOpen}
    title="Change Password"
    description="Are you sure you want to change your password? All other active sessions will be signed out."
    confirmLabel="Yes, change password"
    cancelLabel="Cancel"
    onConfirm={mockConfirm}
  />
</div>
