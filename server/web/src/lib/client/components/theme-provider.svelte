<script lang="ts">
  import { onMount } from "svelte";
  import {
    theme,
    getLightTokens,
    getDarkTokens,
    initTheme,
  } from "$lib/client/components/design/theme-state.svelte.js";

  onMount(() => {
    initTheme();
  });

  /**
   * Use `html:root` (specificity 0,1,1) to beat app.css's `:root` (0,1,0).
   * This ensures theme overrides win regardless of <style>/<link> ordering
   * in <head> — <svelte:head> injects before compiled CSS links.
   */
  const overrideCss = $derived.by(() => {
    const light = getLightTokens();
    const dark = getDarkTokens();

    const lightLines = Object.entries(light)
      .map(([k, v]) => `  ${k}: ${v};`)
      .join("\n");
    const darkLines = Object.entries(dark)
      .map(([k, v]) => `  ${k}: ${v};`)
      .join("\n");

    return [
      `html:root {`,
      `  --radius: ${theme.radius}rem;`,
      lightLines,
      `}`,
      `html.dark {`,
      darkLines,
      `}`,
    ].join("\n");
  });
</script>

<svelte:head>
  <!-- eslint-disable-next-line svelte/no-at-html-tags -- safe: overrideCss is derived from our own theme state, not user input -->
  {@html `<style data-theme-provider>${overrideCss}${"</"}style>`}
</svelte:head>
