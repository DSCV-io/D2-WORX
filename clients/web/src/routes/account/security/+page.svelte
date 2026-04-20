<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import ChangePasswordDialog from "$lib/client/components/account/change-password-dialog.svelte";
  import ActiveSessionsCard from "$lib/client/components/account/active-sessions-card.svelte";
  import RecentLoginsCard from "$lib/client/components/account/recent-logins-card.svelte";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";

  let changePasswordOpen = $state(false);
</script>

<svelte:head>
  <title
    >{m.account_page_title()} / {m.account_security_title()} — {m.webclient_nav_brand()}</title
  >
  <meta
    name="description"
    content={m.account_security_description()}
  />
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
      <Button onclick={() => (changePasswordOpen = true)}>
        {m.account_security_change_password_title()}
      </Button>
    </Card.Content>
  </Card.Root>

  <ChangePasswordDialog bind:open={changePasswordOpen} />

  <ActiveSessionsCard />

  <RecentLoginsCard />

  <!-- Danger Zone — Delete Account flow is deferred (tracked in PROFILE_PROGRESS.md) -->
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
