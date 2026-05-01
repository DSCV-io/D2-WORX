<script lang="ts">
  import { resolve } from "$app/paths";
  import * as m from "$lib/paraglide/messages.js";
</script>

<footer class="border-t">
  <div class="mx-auto flex max-w-7xl items-center justify-between px-4 py-6 sm:px-6 lg:px-8">
    <p class="text-muted-foreground text-sm">
      &copy; {new Date().getFullYear()} DCSV. All rights reserved.
    </p>
    <div class="text-muted-foreground flex gap-4 text-sm">
      <a
        href={resolve("/terms")}
        class="hover:text-foreground transition-colors">{m.webclient_legal_terms_link()}</a
      >
      <a
        href={resolve("/privacy")}
        class="hover:text-foreground transition-colors">{m.webclient_legal_privacy_link()}</a
      >
    </div>
  </div>
</footer>
