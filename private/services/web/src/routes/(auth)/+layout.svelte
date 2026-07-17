<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import PublicNav from "$lib/client/components/layout/public-nav.svelte";

  let { children } = $props();
</script>

<div class="flex min-h-screen flex-col">
  <PublicNav />
  <main class="flex flex-1 items-center justify-center px-4 py-8">
    <div class="w-full max-w-md">
      {@render children?.()}
    </div>
  </main>
</div>
