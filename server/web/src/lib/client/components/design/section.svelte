<script lang="ts">
  import type { Snippet } from "svelte";

  let { id, title, children }: { id: string; title: string; children: Snippet } = $props();
</script>

<section
  {id}
  class="scroll-mt-20"
>
  <h2 class="mb-4 text-2xl font-semibold tracking-tight">{title}</h2>
  {@render children()}
</section>
