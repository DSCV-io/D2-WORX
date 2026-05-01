<script lang="ts">
  import PublicNav from "$lib/client/components/layout/public-nav.svelte";
  import PublicFooter from "$lib/client/components/layout/public-footer.svelte";

  let { children } = $props();
</script>

<div class="flex min-h-screen flex-col">
  <PublicNav />
  <main class="mx-auto w-full max-w-7xl flex-1 px-4 py-8 sm:px-6 lg:px-8">
    {@render children?.()}
  </main>
  <PublicFooter />
</div>
