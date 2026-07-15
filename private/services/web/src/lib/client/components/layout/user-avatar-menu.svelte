<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import * as m from "$lib/paraglide/messages.js";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import SegmentedControl from "$lib/client/components/ui/segmented-control.svelte";
  import LanguageModal from "$lib/client/components/layout/language-modal.svelte";
  import TimezoneModal from "$lib/client/components/layout/timezone-modal.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { userPrefersMode, setMode } from "mode-watcher";
  import { builtInPresets } from "$lib/client/components/design/theme-presets.js";
  import {
    applyPreset,
    getActivePresetName,
  } from "$lib/client/components/design/theme-state.svelte.js";
  import UserRoundCogIcon from "@lucide/svelte/icons/user-round-cog";
  import LogOutIcon from "@lucide/svelte/icons/log-out";
  import LanguagesIcon from "@lucide/svelte/icons/languages";
  import ClockIcon from "@lucide/svelte/icons/clock";
  import SunIcon from "@lucide/svelte/icons/sun";
  import MoonIcon from "@lucide/svelte/icons/moon";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import { cn } from "$lib/shared/utils/utils.js";
  import { getAvatarDisplayUrl } from "$lib/client/utils/avatar-url.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { page } from "$app/stores";
  import { getLocale } from "$lib/paraglide/runtime";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";
  import type { TimezoneOption } from "$lib/shared/forms/timezone-options.js";

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
      locale?: string;
      timezone?: string;
    };
    onSignOut: () => Promise<void> | void;
    size?: "sm" | "md" | "lg";
    class?: string;
  } = $props();

  const sizeClasses = $derived(size === "sm" ? "size-7" : size === "lg" ? "size-10" : "size-8");

  // Resolve user.image (fileId) to a presigned display URL
  let avatarUrl: string | undefined = $state();
  // svelte-ignore state_referenced_locally
  // Initial-only seed so the skeleton renders immediately on SSR when an image
  // is set. The $effect on `user.image` below keeps it in sync.
  let avatarLoading = $state(!!user.image);

  $effect(() => {
    if (user.image) {
      avatarLoading = true;
      getAvatarDisplayUrl(user.image, "small")
        .then((url) => {
          avatarUrl = url;
          avatarLoading = false;
        })
        .catch(() => {
          avatarUrl = undefined;
          avatarLoading = false;
        });
    } else {
      avatarUrl = undefined;
      avatarLoading = false;
    }
  });

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

  // --- Language & timezone display labels ---
  const localeOptions: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );
  const timezoneOptions: TimezoneOption[] = $derived(
    ($page.data as { timezoneOptions?: TimezoneOption[] }).timezoneOptions ?? [],
  );

  const currentLocaleCode = $derived(user.locale ?? getLocale());
  const currentLocaleLabel = $derived(
    localeOptions.find((l) => l.code === currentLocaleCode)?.endonym ?? currentLocaleCode,
  );

  const currentTimezoneCode = $derived(
    user.timezone ?? Intl.DateTimeFormat().resolvedOptions().timeZone,
  );
  const currentTimezoneLabel = $derived(
    timezoneOptions.find((t) => t.value === currentTimezoneCode)?.displayName ??
      currentTimezoneCode.replace(/_/g, " ").replace(/\//g, " / "),
  );

  // --- Language & timezone modals ---
  let languageModalOpen = $state(false);
  let timezoneModalOpen = $state(false);
</script>

<LanguageModal bind:open={languageModalOpen} />
<TimezoneModal bind:open={timezoneModalOpen} />

<DropdownMenu.Root>
  <DropdownMenu.Trigger>
    {#snippet child({ props })}
      <Button
        variant="ghost"
        class={cn("rounded-full p-0", sizeClasses, className)}
        {...props}
      >
        {#if avatarLoading}
          <Skeleton class={cn(sizeClasses, "rounded-full")} />
        {:else}
          {#key avatarUrl}
            <Avatar.Root class={cn(sizeClasses, "rounded-full")}>
              {#if avatarUrl}
                <Avatar.Image
                  src={avatarUrl}
                  alt={user.name ?? m.common_ui_user_avatar_alt()}
                />
              {/if}
              <Avatar.Fallback
                class="rounded-full text-xs font-medium text-white"
                style="background-color: {avatarColor()}"
              >
                {initials()}
              </Avatar.Fallback>
            </Avatar.Root>
          {/key}
        {/if}
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
        <p class="text-sm leading-none font-medium">{user.name ?? m.common_ui_user_fallback()}</p>
        {#if user.email}
          <p class="text-muted-foreground text-xs leading-none">{user.email}</p>
        {/if}
      </div>
    </DropdownMenu.Label>

    <DropdownMenu.Separator />

    <DropdownMenu.Item onSelect={() => goto(resolve("/account/profile"))}>
      <UserRoundCogIcon class="mr-2 size-4" />
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
      {currentLocaleLabel}
    </DropdownMenu.Item>

    <!-- Timezone -->
    <DropdownMenu.Item onSelect={() => (timezoneModalOpen = true)}>
      <ClockIcon class="mr-2 size-4" />
      {currentTimezoneLabel}
    </DropdownMenu.Item>

    <DropdownMenu.Separator />

    <!-- Sign out -->
    <DropdownMenu.Item onSelect={() => onSignOut()}>
      <LogOutIcon class="mr-2 size-4" />
      {m.common_ui_sign_out()}
    </DropdownMenu.Item>
  </DropdownMenu.Content>
</DropdownMenu.Root>
