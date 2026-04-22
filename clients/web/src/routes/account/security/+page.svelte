<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import ChangePasswordDialog from "$lib/client/components/account/change-password-dialog.svelte";
  import ActiveSessionsCard from "$lib/client/components/account/active-sessions-card.svelte";
  import RecentLoginsCard from "$lib/client/components/account/recent-logins-card.svelte";
  import DeleteUserDialog from "$lib/client/components/account/delete-user-dialog.svelte";
  import * as m from "$lib/paraglide/messages.js";

  let changePasswordOpen = $state(false);
  let deleteUserOpen = $state(false);
</script>

<svelte:head>
  <title>{m.account_page_title()} / {m.account_security_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.account_security_description()}
  />
</svelte:head>

<div class="space-y-12">
  <header>
    <h1 class="text-2xl font-semibold tracking-tight">{m.account_security_title()}</h1>
    <p class="text-muted-foreground mt-1 text-sm">{m.account_security_description()}</p>
  </header>

  <Separator class="bg-border/50" />

  <section>
    <div>
      <h2 class="text-base font-semibold">{m.account_security_change_password_title()}</h2>
      <p class="text-muted-foreground mt-0.5 text-sm">
        {m.account_security_change_password_description()}
      </p>
    </div>
    <div class="mt-5">
      <Button onclick={() => (changePasswordOpen = true)}>
        {m.account_security_change_password_title()}
      </Button>
    </div>
  </section>

  <ChangePasswordDialog bind:open={changePasswordOpen} />

  <Separator class="bg-border/50" />

  <ActiveSessionsCard />

  <Separator class="bg-border/50" />

  <RecentLoginsCard />

  <Separator class="bg-border/50" />

  <section>
    <div>
      <h2 class="text-destructive text-base font-semibold">
        {m.account_deactivate_delete_title()}
      </h2>
      <p class="text-muted-foreground mt-0.5 text-sm">
        {m.account_deactivate_delete_description()}
      </p>
    </div>
    <div class="mt-5">
      <Button
        variant="destructive"
        onclick={() => (deleteUserOpen = true)}
      >
        {m.account_deactivate_delete_title()}
      </Button>
    </div>
  </section>

  <DeleteUserDialog bind:open={deleteUserOpen} />
</div>
