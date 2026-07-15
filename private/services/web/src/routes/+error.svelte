<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { resolve } from "$app/paths";
  import { page } from "$app/state";
  import * as m from "$lib/paraglide/messages.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
</script>

<svelte:head>
  <title>{m.webclient_error_page_title()} — {m.webclient_nav_brand()}</title>
</svelte:head>

<div class="flex min-h-[60vh] flex-col items-center justify-center px-4 text-center">
  <h1 class="text-6xl font-bold tracking-tight">
    {page.status}
  </h1>

  <p class="text-muted-foreground mt-4 text-xl">
    {translateMessage(page.error?.message, undefined, m.webclient_error_something_went_wrong())}
  </p>

  {#if page.error?.traceId}
    <p class="text-muted-foreground mt-2 text-sm">
      {m.webclient_error_reference()}
      <code class="bg-muted rounded px-1.5 py-0.5 font-mono text-xs">{page.error.traceId}</code>
    </p>
  {/if}

  <div class="mt-8">
    <a
      href={resolve("/")}
      class="bg-primary text-primary-foreground hover:bg-primary/90 inline-flex items-center rounded-md px-4 py-2 text-sm font-medium"
    >
      {m.webclient_error_go_home()}
    </a>
  </div>
</div>
