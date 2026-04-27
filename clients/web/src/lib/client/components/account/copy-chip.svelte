<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Tooltip from "$lib/client/components/ui/tooltip/index.js";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";

  interface Props {
    /** Short label rendered before the value (e.g. "WhoIs ID"). */
    label: string;
    /** Display text — usually a truncated form of `value`. */
    display: string;
    /** Full text actually copied to the clipboard. */
    value: string | undefined | null;
  }

  let { label, display, value }: Props = $props();

  let copied = $state(false);
  let copyTimeout: ReturnType<typeof setTimeout> | undefined;

  async function handleCopy() {
    if (!value) return;
    try {
      await navigator.clipboard.writeText(value);
      copied = true;
      clearTimeout(copyTimeout);
      // Tooltip flips to "Copied!" briefly, then back to "Click to copy"
      // for the next interaction.
      copyTimeout = setTimeout(() => {
        copied = false;
      }, 1500);
    } catch {
      // Clipboard API unavailable (insecure context, permissions denied, etc.)
      // — silently no-op rather than surface a confusing error.
    }
  }
</script>

<Tooltip.Provider delayDuration={150}>
  <Tooltip.Root>
    <Tooltip.Trigger>
      {#snippet child({ props })}
        <button
          {...props}
          type="button"
          onclick={handleCopy}
          disabled={!value}
          class="text-muted-foreground hover:text-foreground hover:bg-muted/50 hover:border-border focus-visible:ring-ring inline-flex items-center gap-1.5 rounded-md border border-transparent px-1.5 py-0.5 font-mono text-[10px] transition-colors focus-visible:ring-2 focus-visible:outline-none disabled:cursor-not-allowed disabled:opacity-60"
          aria-label={`${label}: ${display}. ${m.common_ui_click_to_copy()}`}
        >
          <span class="text-foreground/70 font-sans text-[10px] font-medium">{label}:</span>
          <span>{display}</span>
          {#if copied}
            <CheckIcon class="text-success size-3" />
          {:else}
            <CopyIcon class="size-3 opacity-60" />
          {/if}
        </button>
      {/snippet}
    </Tooltip.Trigger>
    <Tooltip.Content>
      <p class="text-xs">{copied ? m.common_ui_copied() : m.common_ui_click_to_copy()}</p>
    </Tooltip.Content>
  </Tooltip.Root>
</Tooltip.Provider>
