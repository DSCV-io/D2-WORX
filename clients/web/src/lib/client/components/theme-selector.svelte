<script lang="ts">
  import PaletteIcon from "@lucide/svelte/icons/palette";
  import CheckIcon from "@lucide/svelte/icons/check";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import {
    builtInPresets,
    presetSwatchColor,
  } from "$lib/client/components/design/theme-presets.js";
  import {
    applyPreset,
    getActivePresetName,
  } from "$lib/client/components/design/theme-state.svelte.js";
  import * as m from "$lib/paraglide/messages.js";
</script>

<DropdownMenu.Root>
  <DropdownMenu.Trigger>
    {#snippet child({ props })}
      <Button
        {...props}
        variant="ghost"
        size="icon"
        aria-label={m.common_ui_select_theme()}
      >
        <PaletteIcon class="size-4" />
      </Button>
    {/snippet}
  </DropdownMenu.Trigger>
  <DropdownMenu.Content align="end">
    {#each builtInPresets as preset (preset.name)}
      <DropdownMenu.Item onclick={() => applyPreset(preset)}>
        <span
          class="border-border mr-2 size-4 shrink-0 rounded-full border"
          style="background: {presetSwatchColor(preset)}"
        ></span>
        {preset.name}
        {#if getActivePresetName() === preset.name}
          <CheckIcon class="ml-auto size-4" />
        {/if}
      </DropdownMenu.Item>
    {/each}
  </DropdownMenu.Content>
</DropdownMenu.Root>
