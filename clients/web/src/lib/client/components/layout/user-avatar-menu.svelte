<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as m from "$lib/paraglide/messages.js";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import SegmentedControl from "$lib/client/components/ui/segmented-control.svelte";
  import LanguageModal from "$lib/client/components/layout/language-modal.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { userPrefersMode, setMode } from "mode-watcher";
  import { builtInPresets } from "$lib/client/components/design/theme-presets.js";
  import {
    applyPreset,
    getActivePresetName,
  } from "$lib/client/components/design/theme-state.svelte.js";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import LogOutIcon from "@lucide/svelte/icons/log-out";
  import LanguagesIcon from "@lucide/svelte/icons/languages";
  import SunIcon from "@lucide/svelte/icons/sun";
  import MoonIcon from "@lucide/svelte/icons/moon";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import { cn } from "$lib/shared/utils/utils.js";

  let {
    user,
    onSignOut,
    size = "md",
    class: className,
  }: {
    user: {
      id: string;
      name?: string;
      email?: string;
      image?: string;
    };
    onSignOut: () => Promise<void> | void;
    size?: "sm" | "md" | "lg";
    class?: string;
  } = $props();

  const sizeClasses = $derived(size === "sm" ? "size-7" : size === "lg" ? "size-10" : "size-8");

  const initials = $derived(() => {
    if (!user.name) return "?";
    const parts = user.name.trim().split(/\s+/);
    if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    return parts[0][0]?.toUpperCase() ?? "?";
  });

  const avatarColor = $derived(() => {
    let hash = 0;
    for (const char of user.id) {
      hash = (hash * 31 + char.charCodeAt(0)) | 0;
    }
    const hue = ((hash % 360) + 360) % 360;
    return `hsl(${hue}, 55%, 45%)`;
  });

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

<DropdownMenu.Root>
  <DropdownMenu.Trigger>
    {#snippet child({ props })}
      <Button
        variant="ghost"
        class={cn("rounded-full p-0", sizeClasses, className)}
        {...props}
      >
        <Avatar.Root class={cn(sizeClasses, "rounded-full")}>
          {#if user.image}
            <Avatar.Image
              src={user.image}
              alt={user.name ?? "User"}
            />
          {/if}
          <Avatar.Fallback
            class="rounded-full text-xs font-medium text-white"
            style="background-color: {avatarColor()}"
          >
            {initials()}
          </Avatar.Fallback>
        </Avatar.Root>
      </Button>
    {/snippet}
  </DropdownMenu.Trigger>

  <DropdownMenu.Content
    align="end"
    class="w-72"
  >
    <!-- User info -->
    <DropdownMenu.Label class="font-normal">
      <div class="flex flex-col gap-1">
        <p class="text-sm leading-none font-medium">{user.name ?? "User"}</p>
        {#if user.email}
          <p class="text-muted-foreground text-xs leading-none">{user.email}</p>
        {/if}
      </div>
    </DropdownMenu.Label>

    <DropdownMenu.Separator />

    <DropdownMenu.Item onSelect={() => goto(resolve("/account/profile"))}>
      <SettingsIcon class="mr-2 size-4" />
      {m.common_ui_account()}
    </DropdownMenu.Item>

    <DropdownMenu.Separator />

    <!-- Mode -->
    <div class="px-1 py-1.5">
      <p class="text-muted-foreground mb-1.5 px-2 text-xs font-medium">{m.common_ui_mode()}</p>
      <SegmentedControl
        segments={modeSegments}
        value={currentMode}
        onchange={handleModeChange}
        size="sm"
        class="w-full"
      />
    </div>

    <!-- Theme -->
    <div class="px-1 py-1.5">
      <p class="text-muted-foreground mb-1.5 px-2 text-xs font-medium">{m.common_ui_theme()}</p>
      <SegmentedControl
        segments={themeSegments}
        value={currentTheme}
        onchange={handleThemeChange}
        size="sm"
        class="w-full"
      />
    </div>

    <DropdownMenu.Separator />

    <!-- Language -->
    <DropdownMenu.Item onSelect={() => (languageModalOpen = true)}>
      <LanguagesIcon class="mr-2 size-4" />
      {m.common_ui_language()}
    </DropdownMenu.Item>

    <DropdownMenu.Separator />

    <!-- Sign out -->
    <DropdownMenu.Item onSelect={() => onSignOut()}>
      <LogOutIcon class="mr-2 size-4" />
      {m.common_ui_sign_out()}
    </DropdownMenu.Item>
  </DropdownMenu.Content>
</DropdownMenu.Root>
