<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.webclient_debug_account_components_deactivate_page_title()}</title>
  <meta
    name="description"
    content={m.webclient_debug_account_components_deactivate_page_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">{m.webclient_app_account_deactivate_title()}</h2>
    <p class="text-muted-foreground text-sm">
      {m.webclient_app_account_deactivate_description()}
    </p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">{m.webclient_app_account_deactivate_delete_title()}</Card.Title>
      <Card.Description>
        {m.webclient_app_account_deactivate_delete_description()}
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Button
        variant="destructive"
        onclick={() => toast.error(m.webclient_app_account_deactivate_not_available())}
      >
        {m.webclient_app_account_deactivate_delete_title()}
      </Button>
    </Card.Content>
  </Card.Root>
</div>
