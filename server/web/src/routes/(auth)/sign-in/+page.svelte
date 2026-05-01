<script lang="ts">
  import { page } from "$app/state";
  import { SignInForm } from "$lib/client/components/auth/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import * as m from "$lib/paraglide/messages.js";

  let { data } = $props();

  const returnTo = $derived(page.url.searchParams.get("returnTo"));
</script>

<svelte:head>
  <title>{m.auth_sign_in_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.auth_sign_in_description()}
  />
  <meta
    property="og:title"
    content="{m.auth_sign_in_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.auth_sign_in_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<Card.Root>
  <Card.Header>
    <Card.Title class="text-2xl">{m.auth_sign_in_title()}</Card.Title>
    <Card.Description>{m.auth_sign_in_description()}</Card.Description>
  </Card.Header>
  <Card.Content>
    <SignInForm
      data={data.form}
      {returnTo}
    />
  </Card.Content>
</Card.Root>
