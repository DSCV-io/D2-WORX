<script lang="ts">
  import Section from "./section.svelte";
  import ColorSwatch from "./color-swatch.svelte";
  import { getLightTokens, getDarkTokens } from "./theme-state.svelte.js";
  import { SEMANTIC_TOKENS, CHART_TOKENS } from "./theme-utils.js";

  const STATUS_TOKENS = [
    "destructive",
    "destructive-foreground",
    "info",
    "info-foreground",
    "success",
    "success-foreground",
    "warning",
    "warning-foreground",
  ] as const;

  const CORE_TOKENS = SEMANTIC_TOKENS.filter(
    (t) => !STATUS_TOKENS.includes(t as (typeof STATUS_TOKENS)[number]),
  );
</script>

<Section
  id="colors"
  title="Colors"
>
  <div class="flex flex-col gap-6">
    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Semantic Tokens (Light)</h3>
      <div class="grid grid-cols-4 gap-3 sm:grid-cols-5 md:grid-cols-7">
        {#each CORE_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getLightTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>

    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Semantic Tokens (Dark)</h3>
      <div class="grid grid-cols-4 gap-3 sm:grid-cols-5 md:grid-cols-7">
        {#each CORE_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getDarkTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>

    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Status Colors (Light)</h3>
      <div class="grid grid-cols-4 gap-3 sm:grid-cols-5 md:grid-cols-8">
        {#each STATUS_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getLightTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>

    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Status Colors (Dark)</h3>
      <div class="grid grid-cols-4 gap-3 sm:grid-cols-5 md:grid-cols-8">
        {#each STATUS_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getDarkTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>

    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Chart Colors (Light)</h3>
      <div class="grid grid-cols-5 gap-3">
        {#each CHART_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getLightTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>

    <div>
      <h3 class="text-muted-foreground mb-3 text-sm font-medium">Chart Colors (Dark)</h3>
      <div class="grid grid-cols-5 gap-3">
        {#each CHART_TOKENS as token (token)}
          <ColorSwatch
            name={token}
            value={getDarkTokens()[`--${token}`]}
          />
        {/each}
      </div>
    </div>
  </div>
</Section>
