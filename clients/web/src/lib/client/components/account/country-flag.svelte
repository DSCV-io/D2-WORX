<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";

  interface Props {
    /** ISO 3166-1 alpha-2 (e.g. "US", "GB"). Case-insensitive. */
    code: string | undefined | null;
    /** Display name for the flag's accessible alt text (e.g. "United States"). Falls back to the code. */
    countryName?: string;
    /** CSS class. Defaults to a small inline 4:3 size. */
    class?: string;
  }

  let { code, countryName, class: classProp = "" }: Props = $props();

  // Lowercase ISO code matches the asset filenames under /static/flags/4x3/.
  const normalized = $derived(code?.toLowerCase().trim());
  const altText = $derived(
    m.common_ui_country_flag_alt({ country: countryName ?? code?.toUpperCase() ?? "" }),
  );
</script>

{#if normalized}
  <img
    src={`/flags/4x3/${normalized}.svg`}
    alt={altText}
    class={`inline-block h-3 w-4 rounded-[1px] object-cover align-text-bottom ${classProp}`.trim()}
    loading="lazy"
    decoding="async"
  />
{/if}
