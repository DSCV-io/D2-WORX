<script lang="ts">
  import ConfirmationDialog from "$lib/client/components/ui/confirmation-dialog.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";

  let confirmDialogOpen = $state(false);

  async function mockConfirm() {
    await new Promise((r) => setTimeout(r, 1500));
    toast.success(m.account_security_password_changed());
  }
</script>

<svelte:head>
  <title
    >{m.account_page_title()} / {m.account_security_title()} — {m.webclient_nav_brand()}</title
  >
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.account_security_title()}</h2>
    <p class="text-muted-foreground text-sm">{m.account_security_description()}</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_security_change_password_title()}</Card.Title>
      <Card.Description>{m.account_security_change_password_description()}</Card.Description>
    </Card.Header>
    <Card.Content>
      <Button onclick={() => (confirmDialogOpen = true)}>
        {m.account_security_change_password_title()}
      </Button>
    </Card.Content>
  </Card.Root>

  <ConfirmationDialog
    bind:open={confirmDialogOpen}
    title={m.account_security_change_password_title()}
    description={m.account_security_change_password_confirm()}
    confirmLabel={m.account_security_change_password_yes()}
    onConfirm={mockConfirm}
  />

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_sessions_title()}</Card.Title>
      <Card.Description>{m.account_sessions_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-3">
      <p class="text-muted-foreground text-sm">{m.account_sessions_placeholder()}</p>
      <Button
        variant="outline"
        onclick={() => toast.info(m.account_sessions_sign_out_others())}
      >
        {m.account_sessions_sign_out_others()}
      </Button>
    </Card.Content>
  </Card.Root>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.account_recent_logins_title()}</Card.Title>
      <Card.Description>{m.account_recent_logins_description()}</Card.Description>
    </Card.Header>
    <Card.Content>
      <p class="text-muted-foreground text-sm">{m.account_recent_logins_placeholder()}</p>
    </Card.Content>
  </Card.Root>

  <!-- Danger Zone -->
  <Card.Root class="border-destructive/50">
    <Card.Header>
      <Card.Title class="text-destructive text-base">
        {m.account_deactivate_delete_title()}
      </Card.Title>
      <Card.Description>{m.account_deactivate_delete_description()}</Card.Description>
    </Card.Header>
    <Card.Content>
      <Button
        variant="destructive"
        onclick={() => toast.error(m.account_deactivate_not_available())}
      >
        {m.account_deactivate_delete_title()}
      </Button>
    </Card.Content>
  </Card.Root>
</div>
