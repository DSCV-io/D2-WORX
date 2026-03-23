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
    <p class="text-muted-foreground text-sm">
      Manage your password, active sessions, and review login history.
    </p>
  </div>

  <!-- Change Password -->
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
    onConfirm={mockConfirm}
  />

  <!-- Active Sessions -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Active Sessions</Card.Title>
      <Card.Description>Devices and browsers where you are currently signed in.</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-3">
      <p class="text-muted-foreground text-sm">Session list will appear here.</p>
      <Button
        variant="outline"
        size="sm"
        onclick={() => toast.info("Sign out all others (simulated)")}
      >
        Sign out all other sessions
      </Button>
    </Card.Content>
  </Card.Root>

  <!-- Recent Logins -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Recent Logins</Card.Title>
      <Card.Description>Review your sign-in history and approximate locations.</Card.Description>
    </Card.Header>
    <Card.Content>
      <p class="text-muted-foreground text-sm">Login history and map will appear here.</p>
    </Card.Content>
  </Card.Root>
</div>
