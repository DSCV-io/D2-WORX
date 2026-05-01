<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
</script>

<svelte:head>
  <title>{m.webclient_legal_terms_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_legal_terms_description()}
  />
  <meta
    property="og:title"
    content={m.webclient_legal_terms_title()}
  />
  <meta
    property="og:description"
    content={m.webclient_legal_terms_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="prose dark:prose-invert mx-auto max-w-3xl py-8">
  <h1>{m.webclient_legal_terms_heading()}</h1>
  <p class="text-muted-foreground">{m.webclient_legal_terms_placeholder()}</p>
</div>
