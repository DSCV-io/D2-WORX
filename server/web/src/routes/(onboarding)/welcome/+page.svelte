<script lang="ts">
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.webclient_onboarding_welcome_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_onboarding_welcome_description()}
  />
  <meta
    property="og:title"
    content="{m.webclient_onboarding_welcome_title()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_onboarding_welcome_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<Card.Card>
  <Card.CardHeader class="text-center">
    <Card.CardTitle class="text-2xl">{m.webclient_onboarding_welcome_heading()}</Card.CardTitle>
    <Card.CardDescription>
      {m.webclient_onboarding_welcome_setup_message()}
    </Card.CardDescription>
  </Card.CardHeader>
  <Card.CardContent>
    <p class="text-muted-foreground text-center text-sm">
      {m.webclient_onboarding_welcome_placeholder()}
    </p>
  </Card.CardContent>
  <Card.CardFooter class="justify-center">
    <Button disabled>{m.webclient_onboarding_create_org()}</Button>
  </Card.CardFooter>
</Card.Card>
