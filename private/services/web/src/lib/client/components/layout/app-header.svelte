<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { SidebarTrigger } from "$lib/client/components/ui/sidebar/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import * as Breadcrumb from "$lib/client/components/ui/breadcrumb/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import SegmentedControl from "$lib/client/components/ui/segmented-control.svelte";
  import LanguageModal from "$lib/client/components/layout/language-modal.svelte";
  import { userPrefersMode, setMode } from "mode-watcher";
  import { builtInPresets } from "$lib/client/components/design/theme-presets.js";
  import {
    applyPreset,
    getActivePresetName,
  } from "$lib/client/components/design/theme-state.svelte.js";
  import * as m from "$lib/paraglide/messages.js";
  import EllipsisVerticalIcon from "@lucide/svelte/icons/ellipsis-vertical";
  import SunIcon from "@lucide/svelte/icons/sun";
  import MoonIcon from "@lucide/svelte/icons/moon";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import LanguagesIcon from "@lucide/svelte/icons/languages";

  // --- Mode (read from source of truth, write via onchange) ---
  const modeSegments = $derived([
    { value: "light", label: m.common_ui_mode_light(), icon: SunIcon },
    { value: "system", label: m.common_ui_mode_system(), icon: MonitorIcon },
    { value: "dark", label: m.common_ui_mode_dark(), icon: MoonIcon },
  ]);

  const currentMode = $derived(userPrefersMode.current ?? "system");

  function handleModeChange(value: string) {
    setMode(value as "light" | "dark" | "system");
  }

  // --- Theme (read from source of truth, write via onchange) ---
  const themeSegments = $derived(builtInPresets.map((p) => ({ value: p.name, label: p.name })));

  const currentTheme = $derived(getActivePresetName() ?? builtInPresets[0]?.name ?? "");

  function handleThemeChange(value: string) {
    const preset = builtInPresets.find((p) => p.name === value);
    if (preset) applyPreset(preset);
  }

  // --- Language modal ---
  let languageModalOpen = $state(false);
</script>

<LanguageModal bind:open={languageModalOpen} />

<header class="bg-background sticky top-0 z-10 flex h-14 shrink-0 items-center gap-2 border-b px-4">
  <div class="flex items-center gap-2">
    <SidebarTrigger class="-ml-1" />
    <Separator
      orientation="vertical"
      class="mr-2 h-4"
    />
    <Breadcrumb.Breadcrumb>
      <Breadcrumb.BreadcrumbList>
        <Breadcrumb.BreadcrumbItem>
          <Breadcrumb.BreadcrumbPage>{m.common_ui_dashboard()}</Breadcrumb.BreadcrumbPage>
        </Breadcrumb.BreadcrumbItem>
      </Breadcrumb.BreadcrumbList>
    </Breadcrumb.Breadcrumb>
  </div>

  <div class="ml-auto flex items-center gap-2">
    <DropdownMenu.Root>
      <DropdownMenu.Trigger>
        {#snippet child({ props })}
          <Button
            {...props}
            variant="ghost"
            size="icon"
            aria-label={m.common_ui_preferences()}
          >
            <EllipsisVerticalIcon class="size-4" />
          </Button>
        {/snippet}
      </DropdownMenu.Trigger>
      <DropdownMenu.Content
        align="end"
        class="w-72"
      >
        <div class="px-1 py-1.5">
          <p class="text-muted-foreground mb-1.5 px-2 text-xs font-medium">
            {m.common_ui_mode()}
          </p>
          <SegmentedControl
            segments={modeSegments}
            value={currentMode}
            onchange={handleModeChange}
            size="sm"
            class="w-full"
          />
        </div>
        <div class="px-1 py-1.5">
          <p class="text-muted-foreground mb-1.5 px-2 text-xs font-medium">
            {m.common_ui_theme()}
          </p>
          <SegmentedControl
            segments={themeSegments}
            value={currentTheme}
            onchange={handleThemeChange}
            size="sm"
            class="w-full"
          />
        </div>
        <DropdownMenu.Separator />
        <DropdownMenu.Item onSelect={() => (languageModalOpen = true)}>
          <LanguagesIcon class="mr-2 size-4" />
          {m.common_ui_language()}
        </DropdownMenu.Item>
      </DropdownMenu.Content>
    </DropdownMenu.Root>
  </div>
</header>
