<script lang="ts">
  import { page } from "$app/stores";
  import { resolve } from "$app/paths";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import MailIcon from "@lucide/svelte/icons/mail";
  import * as m from "$lib/paraglide/messages.js";

  const email = $derived($page.url.searchParams.get("email") ?? "");
  const resent = $derived($page.url.searchParams.get("resent") === "true");
</script>

<svelte:head>
  <title>{m.auth_verify_email_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.auth_verify_email_description()}
  />
  <meta
    name="robots"
    content="noindex"
  />
  <meta
    property="og:title"
    content="{m.auth_verify_email_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.auth_verify_email_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<Card.Root>
  <Card.Header class="text-center">
    <div class="bg-muted mx-auto mb-2 flex size-12 items-center justify-center rounded-full">
      <MailIcon class="text-muted-foreground size-6" />
    </div>
    <Card.Title class="text-2xl">{m.auth_verify_email_check_email()}</Card.Title>
  </Card.Header>
  <Card.Content class="flex flex-col gap-3 text-center">
    {#if resent}
      <p class="text-sm">
        {m.auth_verify_email_resent_message()}
      </p>
    {:else}
      <p class="text-sm">{m.auth_verify_email_sent_message()}</p>
    {/if}
    {#if email}
      <p class="font-medium">{email}</p>
    {/if}
    <p class="text-muted-foreground text-sm">
      {m.auth_verify_email_activate_message()}
    </p>
  </Card.Content>
  <Card.Footer>
    <Button
      variant="outline"
      href={resolve("/sign-in")}
      class="w-full">{m.auth_verify_email_back_to_sign_in()}</Button
    >
  </Card.Footer>
</Card.Root>
