<script lang="ts">
  import { page } from "$app/state";
  import { ResetPasswordForm } from "$lib/client/components/auth/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import * as m from "$lib/paraglide/messages.js";

  let { data } = $props();

  const token = $derived(page.url.searchParams.get("token"));
</script>

<svelte:head>
  <title>{m.auth_reset_password_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.auth_reset_password_description()}
  />
  <meta
    name="robots"
    content="noindex"
  />
  <meta
    property="og:title"
    content="{m.auth_reset_password_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.auth_reset_password_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<Card.Root>
  <ResetPasswordForm
    data={data.form}
    {token}
  />
</Card.Root>
