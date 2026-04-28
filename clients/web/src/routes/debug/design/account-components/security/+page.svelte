<script lang="ts">
  import ConfirmationDialog from "$lib/client/components/ui/confirmation-dialog.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";

  let confirmDialogOpen = $state(false);

  async function mockConfirm() {
    await new Promise((r) => setTimeout(r, 1500));
    toast.success(m.webclient_debug_account_components_password_changed_simulated());
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_account_components_security_page_title()}</title>
  <meta
    name="description"
    content={m.webclient_debug_account_components_security_page_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.webclient_app_account_security_title()}</h2>
    <p class="text-muted-foreground text-sm">
      {m.webclient_app_account_security_description()}
    </p>
  </div>

  <!-- Change Password -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">
        {m.webclient_app_account_security_change_password_title()}
      </Card.Title>
      <Card.Description>
        {m.webclient_app_account_security_change_password_description()}
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Button onclick={() => (confirmDialogOpen = true)}>
        {m.webclient_app_account_security_change_password_title()}
      </Button>
    </Card.Content>
  </Card.Root>

  <ConfirmationDialog
    bind:open={confirmDialogOpen}
    title={m.webclient_app_account_security_change_password_title()}
    description={m.webclient_app_account_security_change_password_confirm()}
    confirmLabel={m.webclient_app_account_security_change_password_yes()}
    onConfirm={mockConfirm}
  />

  <!-- Active Sessions -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_sessions_title()}</Card.Title>
      <Card.Description>{m.webclient_app_account_sessions_description()}</Card.Description>
    </Card.Header>
    <Card.Content class="space-y-3">
      <p class="text-muted-foreground text-sm">{m.webclient_app_account_sessions_placeholder()}</p>
      <Button
        variant="outline"
        size="sm"
        onclick={() => toast.info(m.webclient_debug_account_components_signout_others_simulated())}
      >
        {m.webclient_app_account_sessions_sign_out_others()}
      </Button>
    </Card.Content>
  </Card.Root>

  <!-- Recent Logins -->
  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_recent_logins_title()}</Card.Title>
      <Card.Description>{m.webclient_app_account_recent_logins_description()}</Card.Description>
    </Card.Header>
    <Card.Content>
      <p class="text-muted-foreground text-sm">
        {m.webclient_app_account_recent_logins_placeholder()}
      </p>
    </Card.Content>
  </Card.Root>
</div>
