<script lang="ts">
  import { resolve } from "$app/paths";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import CircleAlertIcon from "@lucide/svelte/icons/circle-alert";
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.account_delete_scheduled_page_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.account_delete_scheduled_page_body()}
  />
  <meta
    name="robots"
    content="noindex"
  />
  <meta
    property="og:title"
    content="{m.account_delete_scheduled_page_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.account_delete_scheduled_page_body()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="relative flex min-h-screen items-center justify-center px-4">
  <div class="w-full max-w-md">
    <div class="mb-8 flex justify-center">
      <a
        href={resolve("/")}
        class="flex items-center gap-2"
      >
        <div
          class="bg-primary text-primary-foreground flex size-8 items-center justify-center rounded-lg text-sm font-bold"
        >
          DW
        </div>
        <span class="text-xl font-semibold">{m.webclient_nav_brand()}</span>
      </a>
    </div>

    <Card.Root>
      <Card.Header class="text-center">
        <div
          class="bg-destructive/10 mx-auto mb-2 flex size-12 items-center justify-center rounded-full"
        >
          <CircleAlertIcon class="text-destructive size-6" />
        </div>
        <Card.Title class="text-2xl">
          {m.account_delete_scheduled_page_title()}
        </Card.Title>
      </Card.Header>
      <Card.Content class="text-center">
        <p class="text-sm">{m.account_delete_scheduled_page_body()}</p>
      </Card.Content>
      <Card.Footer>
        <Button
          href={resolve("/")}
          class="w-full"
        >
          {m.account_delete_scheduled_page_signin_link()}
        </Button>
      </Card.Footer>
    </Card.Root>
  </div>
</div>
