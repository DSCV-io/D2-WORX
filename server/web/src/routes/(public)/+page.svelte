<script lang="ts">
  import { resolve } from "$app/paths";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.webclient_nav_brand()} — {m.webclient_hero_tagline()}</title>
  <meta
    name="description"
    content={m.webclient_hero_tagline()}
  />
  <meta
    property="og:title"
    content={m.webclient_nav_brand()}
  />
  <meta
    property="og:description"
    content={m.webclient_hero_tagline()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="flex flex-col items-center justify-center py-20 text-center">
  <h1 class="text-4xl font-bold tracking-tight sm:text-5xl">{m.webclient_hero_title()}</h1>
  <p class="text-muted-foreground mt-4 max-w-lg text-lg">
    {m.webclient_hero_tagline()}
  </p>
  <div class="mt-8">
    <Button
      href={resolve("/sign-up")}
      size="lg">{m.common_ui_get_started()}</Button
    >
  </div>
</div>
