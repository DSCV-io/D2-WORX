<script lang="ts">
  import {
    Sheet,
    SheetContent,
    SheetHeader,
    SheetTitle,
    SheetDescription,
  } from "$lib/client/components/ui/sheet/index.js";
  import { Slider } from "$lib/client/components/ui/slider/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import ColorPicker from "svelte-awesome-color-picker";
  import {
    presets,
    presetSwatchColor,
    loadCustomPresets,
    saveCustomPreset,
    deleteCustomPreset,
  } from "./theme-presets.js";
  import { theme, getActivePresetName, applyPreset, reset } from "./theme-state.svelte.js";
  import { oklchToHex, hexToOklch } from "./color-convert.js";
  import ExportDialog from "./export-dialog.svelte";
  import RotateCcwIcon from "@lucide/svelte/icons/rotate-ccw";
  import DownloadIcon from "@lucide/svelte/icons/download";
  import SaveIcon from "@lucide/svelte/icons/save";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
  import { onMount } from "svelte";

  let { open = $bindable(false) }: { open?: boolean } = $props();
  let exportOpen = $state(false);
  let saveName = $state("");
  let showSaveInput = $state(false);
  let allPresets = $state(presets);

  onMount(() => {
    loadCustomPresets();
    allPresets = presets;
  });

  // --- Derived hex values from OKLCH state ---

  const primaryHex = $derived(
    oklchToHex(theme.primaryLightness, theme.primaryChroma, theme.primaryHue),
  );
  const secondaryHex = $derived(
    oklchToHex(theme.secondaryLightness, theme.secondaryChroma, theme.secondaryHue),
  );
  const accentHex = $derived(
    oklchToHex(theme.accentLightness, theme.accentChroma, theme.accentHue),
  );
  const destructiveHex = $derived(
    oklchToHex(theme.destructiveLightness, theme.destructiveChroma, theme.destructiveHue),
  );
  const infoHex = $derived(oklchToHex(theme.infoLightness, theme.infoChroma, theme.infoHue));
  const successHex = $derived(
    oklchToHex(theme.successLightness, theme.successChroma, theme.successHue),
  );
  const warningHex = $derived(
    oklchToHex(theme.warningLightness, theme.warningChroma, theme.warningHue),
  );

  // --- Color change handlers ---

  function handleColorChange(
    role: "primary" | "secondary" | "accent" | "destructive" | "info" | "success" | "warning",
    event: { hex: string | null },
  ) {
    if (!event.hex) return;
    const oklch = hexToOklch(event.hex);
    if (!oklch) return;

    switch (role) {
      case "primary":
        theme.primaryHue = oklch.hue;
        theme.primaryChroma = oklch.chroma;
        theme.primaryLightness = oklch.lightness;
        break;
      case "secondary":
        theme.secondaryHue = oklch.hue;
        theme.secondaryChroma = oklch.chroma;
        theme.secondaryLightness = oklch.lightness;
        break;
      case "accent":
        theme.accentHue = oklch.hue;
        theme.accentChroma = oklch.chroma;
        theme.accentLightness = oklch.lightness;
        break;
      case "destructive":
        theme.destructiveHue = oklch.hue;
        theme.destructiveChroma = oklch.chroma;
        theme.destructiveLightness = oklch.lightness;
        break;
      case "info":
        theme.infoHue = oklch.hue;
        theme.infoChroma = oklch.chroma;
        theme.infoLightness = oklch.lightness;
        break;
      case "success":
        theme.successHue = oklch.hue;
        theme.successChroma = oklch.chroma;
        theme.successLightness = oklch.lightness;
        break;
      case "warning":
        theme.warningHue = oklch.hue;
        theme.warningChroma = oklch.chroma;
        theme.warningLightness = oklch.lightness;
        break;
    }
  }

  // --- Preset actions ---

  function handleSavePreset() {
    const name = saveName.trim();
    if (!name) return;
    allPresets = saveCustomPreset({
      name,
      primaryHue: theme.primaryHue,
      primaryChroma: theme.primaryChroma,
      primaryLightness: theme.primaryLightness,
      secondaryHue: theme.secondaryHue,
      secondaryChroma: theme.secondaryChroma,
      secondaryLightness: theme.secondaryLightness,
      accentHue: theme.accentHue,
      accentChroma: theme.accentChroma,
      accentLightness: theme.accentLightness,
      destructiveHue: theme.destructiveHue,
      destructiveChroma: theme.destructiveChroma,
      destructiveLightness: theme.destructiveLightness,
      infoHue: theme.infoHue,
      infoChroma: theme.infoChroma,
      infoLightness: theme.infoLightness,
      successHue: theme.successHue,
      successChroma: theme.successChroma,
      successLightness: theme.successLightness,
      warningHue: theme.warningHue,
      warningChroma: theme.warningChroma,
      warningLightness: theme.warningLightness,
      radius: theme.radius,
    });
    saveName = "";
    showSaveInput = false;
  }

  function handleDeletePreset(name: string) {
    allPresets = deleteCustomPreset(name);
  }
</script>

<Sheet bind:open>
  <SheetContent
    side="right"
    class="w-80 overflow-y-auto sm:max-w-80"
  >
    <SheetHeader>
      <SheetTitle>Theme Editor</SheetTitle>
      <SheetDescription>Adjust colors, radius, and presets. Changes are live.</SheetDescription>
    </SheetHeader>

    <div class="flex flex-col gap-6 px-1 pt-2">
      <!-- Presets -->
      <div class="flex flex-col gap-2">
        <span class="text-sm font-medium">Presets</span>
        <div class="flex flex-wrap gap-2">
          {#each allPresets as preset (preset.name)}
            <div class="group relative">
              <button
                class="size-8 rounded-full border-2 transition-all {getActivePresetName() ===
                preset.name
                  ? 'border-ring ring-ring/30 scale-110 ring-2'
                  : 'border-border hover:scale-105'}"
                style="background: {presetSwatchColor(preset)}"
                title={preset.name}
                onclick={() => applyPreset(preset)}
              >
                <span class="sr-only">{preset.name}</span>
              </button>
              {#if !preset.builtIn}
                <button
                  class="bg-destructive text-destructive-foreground absolute -top-1 -right-1 hidden size-4 items-center justify-center rounded-full group-hover:flex"
                  title="Delete {preset.name}"
                  onclick={() => handleDeletePreset(preset.name)}
                >
                  <Trash2Icon class="size-2.5" />
                </button>
              {/if}
            </div>
          {/each}
        </div>
        <div class="flex flex-col gap-1">
          {#if !showSaveInput}
            <Button
              variant="ghost"
              size="sm"
              class="w-fit text-xs"
              onclick={() => (showSaveInput = true)}
            >
              <SaveIcon class="size-3" />
              Save Current as Preset
            </Button>
          {:else}
            <div class="flex gap-1">
              <Input
                bind:value={saveName}
                placeholder="Preset name"
                class="h-7 text-xs"
                onkeydown={(e: KeyboardEvent) => e.key === "Enter" && handleSavePreset()}
              />
              <Button
                size="sm"
                class="h-7 text-xs"
                onclick={handleSavePreset}>Save</Button
              >
              <Button
                variant="ghost"
                size="sm"
                class="h-7 text-xs"
                onclick={() => (showSaveInput = false)}
              >
                Cancel
              </Button>
            </div>
          {/if}
        </div>
      </div>

      <Separator />

      <!-- Brand Colors -->
      <div class="flex flex-col gap-4">
        <span class="text-sm font-medium">Brand Colors</span>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Primary</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={primaryHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Primary"
              onInput={(e) => handleColorChange("primary", e)}
            />
          </div>
        </div>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Secondary</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={secondaryHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Secondary"
              onInput={(e) => handleColorChange("secondary", e)}
            />
          </div>
        </div>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Accent</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={accentHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Accent"
              onInput={(e) => handleColorChange("accent", e)}
            />
          </div>
        </div>
      </div>

      <Separator />

      <!-- Status Colors -->
      <div class="flex flex-col gap-4">
        <span class="text-sm font-medium">Status Colors</span>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Destructive</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={destructiveHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Destructive"
              onInput={(e) => handleColorChange("destructive", e)}
            />
          </div>
        </div>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Info</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={infoHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Info"
              onInput={(e) => handleColorChange("info", e)}
            />
          </div>
        </div>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Success</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={successHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Success"
              onInput={(e) => handleColorChange("success", e)}
            />
          </div>
        </div>

        <div class="flex items-center justify-between">
          <span class="text-muted-foreground text-xs">Warning</span>
          <div class="color-picker-trigger">
            <ColorPicker
              hex={warningHex}
              isAlpha={false}
              isTextInput={false}
              position="responsive"
              label="Warning"
              onInput={(e) => handleColorChange("warning", e)}
            />
          </div>
        </div>
      </div>

      <Separator />

      <!-- Radius -->
      <label class="flex flex-col gap-1.5">
        <div class="flex items-center justify-between">
          <span class="text-sm font-medium">Border Radius</span>
          <div
            class="border-border bg-primary size-6 border"
            style="border-radius: {theme.radius}rem"
          ></div>
        </div>
        <div class="flex justify-between">
          <span class="text-muted-foreground text-xs">Radius</span>
          <span class="text-muted-foreground text-xs tabular-nums"
            >{theme.radius.toFixed(3)}rem</span
          >
        </div>
        <Slider
          type="single"
          bind:value={theme.radius}
          min={0}
          max={1}
          step={0.025}
        />
      </label>

      <Separator />

      <!-- Actions -->
      <div class="flex flex-col gap-2">
        <Button
          onclick={() => (exportOpen = true)}
          class="w-full"
        >
          <DownloadIcon class="size-4" />
          Copy Theme CSS
        </Button>
        <Button
          variant="outline"
          onclick={reset}
          class="w-full"
        >
          <RotateCcwIcon class="size-4" />
          Reset to WORX
        </Button>
      </div>
    </div>
  </SheetContent>
</Sheet>

<ExportDialog bind:open={exportOpen} />

<style>
  /* Color picker trigger: compact swatch button */
  .color-picker-trigger :global(.color-picker) {
    display: flex;
    align-items: center;
  }

  .color-picker-trigger :global(label.color-picker--label) {
    clip: rect(0 0 0 0);
    clip-path: inset(50%);
    height: 1px;
    overflow: hidden;
    position: absolute;
    white-space: nowrap;
    width: 1px;
  }

  .color-picker-trigger :global(button.color-picker--button) {
    width: 2rem;
    height: 2rem;
    border-radius: 0.375rem;
    border: 1px solid var(--border);
    cursor: pointer;
    padding: 0;
  }

  /* Ensure picker popup renders above the Sheet (z-50) */
  .color-picker-trigger :global(.color-picker--picker-wrapper) {
    z-index: 100;
  }
</style>
