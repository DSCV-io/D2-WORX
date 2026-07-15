<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { page } from "$app/stores";
  import { invalidateAll } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import * as Sheet from "$lib/client/components/ui/sheet/index.js";
  import SegmentedControl from "$lib/client/components/ui/segmented-control.svelte";
  import UserAvatarMenu from "$lib/client/components/layout/user-avatar-menu.svelte";
  import LanguageModal from "$lib/client/components/layout/language-modal.svelte";
  import TimezoneModal from "$lib/client/components/layout/timezone-modal.svelte";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { invalidateToken } from "$lib/client/rest/gateway-client.js";
  import { userPrefersMode, setMode } from "mode-watcher";
  import { builtInPresets } from "$lib/client/components/design/theme-presets.js";
  import {
    applyPreset,
    getActivePresetName,
  } from "$lib/client/components/design/theme-state.svelte.js";
  import * as m from "$lib/paraglide/messages.js";
  import EllipsisVerticalIcon from "@lucide/svelte/icons/ellipsis-vertical";
  import MenuIcon from "@lucide/svelte/icons/menu";
  import SunIcon from "@lucide/svelte/icons/sun";
  import MoonIcon from "@lucide/svelte/icons/moon";
  import MonitorIcon from "@lucide/svelte/icons/monitor";
  import LanguagesIcon from "@lucide/svelte/icons/languages";
  import ClockIcon from "@lucide/svelte/icons/clock";
  import UserRoundCogIcon from "@lucide/svelte/icons/user-round-cog";
  import LogOutIcon from "@lucide/svelte/icons/log-out";
  import { getAvatarDisplayUrl } from "$lib/client/utils/avatar-url.js";
  import { getLocale } from "$lib/paraglide/runtime";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";
  import type { TimezoneOption } from "$lib/shared/forms/timezone-options.js";

  // --- Auth state ---
  const session = $derived($page.data.session);
  const user = $derived(
    $page.data.user as {
      id: string;
      name?: string;
      email?: string;
      image?: string;
      locale?: string;
      timezone?: string;
    } | null,
  );

  let signingOut = $state(false);

  async function handleSignOut() {
    signingOut = true;
    try {
      await authClient.signOut();
      invalidateToken();
      await invalidateAll();
    } finally {
      signingOut = false;
    }
  }

  // --- Avatar helpers ---
  const initials = $derived(() => {
    if (!user?.name) return "?";
    const parts = user.name.trim().split(/\s+/);
    if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    return parts[0][0]?.toUpperCase() ?? "?";
  });

  const avatarColor = $derived(() => {
    if (!user) return "hsl(0, 0%, 45%)";
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

  // --- Avatar URL resolution for mobile trigger ---
  let mobileAvatarUrl: string | undefined = $state();

  $effect(() => {
    if (user?.image) {
      getAvatarDisplayUrl(user.image, "thumb")
        .then((url) => {
          mobileAvatarUrl = url;
        })
        .catch(() => {
          mobileAvatarUrl = undefined;
        });
    } else {
      mobileAvatarUrl = undefined;
    }
  });

  // --- Language display label (for unauthenticated dropdown / mobile sheet) ---
  const localeOptions: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );
  const currentLocaleLabel = $derived(
    localeOptions.find((l) => l.code === getLocale())?.endonym ?? getLocale(),
  );

  // --- Timezone display label ---
  const timezoneOptions: TimezoneOption[] = $derived(
    ($page.data as { timezoneOptions?: TimezoneOption[] }).timezoneOptions ?? [],
  );
  const currentTimezoneCode = $derived(
    ($page.data as { timezone?: string }).timezone ??
      Intl.DateTimeFormat().resolvedOptions().timeZone,
  );
  const currentTimezoneLabel = $derived(
    timezoneOptions.find((t) => t.value === currentTimezoneCode)?.displayName ??
      currentTimezoneCode.replace(/_/g, " ").replace(/\//g, " / "),
  );

  // --- Language + timezone modals ---
  let languageModalOpen = $state(false);
  let timezoneModalOpen = $state(false);

  // --- Mobile sheet ---
  let sheetOpen = $state(false);
</script>

<LanguageModal bind:open={languageModalOpen} />
<TimezoneModal bind:open={timezoneModalOpen} />

<nav
  class="bg-background/95 supports-backdrop-filter:bg-background/60 sticky top-0 z-50 w-full shadow-sm backdrop-blur-sm dark:shadow-[0_1px_3px_0_rgb(0_0_0/0.6)]"
>
  <div class="mx-auto flex h-14 max-w-7xl items-center justify-between px-4 sm:px-6 lg:px-8">
    <!-- Brand: icon-only on mobile, full on desktop -->
    <a
      href={resolve("/")}
      class="flex items-center gap-2"
    >
      <div
        class="bg-primary text-primary-foreground flex size-7 items-center justify-center rounded-md text-xs font-bold"
      >
        DW
      </div>
      <span class="hidden text-lg font-semibold md:inline">{m.webclient_nav_brand()}</span>
    </a>

    <!-- Desktop (md+) -->
    <div class="hidden items-center gap-2 md:flex">
      {#if session && user}
        <UserAvatarMenu
          {user}
          onSignOut={handleSignOut}
        />
      {:else}
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
              {currentLocaleLabel}
            </DropdownMenu.Item>
            <DropdownMenu.Item onSelect={() => (timezoneModalOpen = true)}>
              <ClockIcon class="mr-2 size-4" />
              {currentTimezoneLabel}
            </DropdownMenu.Item>
          </DropdownMenu.Content>
        </DropdownMenu.Root>

        <Button
          variant="outline"
          size="sm"
          href={resolve("/sign-in")}>{m.common_ui_sign_in()}</Button
        >
        <Button
          variant="default"
          size="sm"
          href={resolve("/sign-up")}>{m.common_ui_sign_up()}</Button
        >
      {/if}
    </div>

    <!-- Mobile (<md) -->
    <div class="flex items-center gap-1 md:hidden">
      {#if session && user}
        <!-- Authenticated: avatar opens sheet -->
        <button
          type="button"
          class="rounded-full"
          aria-label={m.common_ui_open_menu()}
          onclick={() => (sheetOpen = true)}
        >
          {#key mobileAvatarUrl}
            <Avatar.Root class="size-8 rounded-full">
              {#if mobileAvatarUrl}
                <Avatar.Image
                  src={mobileAvatarUrl}
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
        </button>
      {:else}
        <!-- Unauthenticated: hamburger opens sheet -->
        <Button
          variant="ghost"
          size="icon"
          aria-label={m.common_ui_open_menu()}
          onclick={() => (sheetOpen = true)}
        >
          <MenuIcon class="size-5" />
        </Button>
      {/if}
    </div>
  </div>
</nav>

<!-- Mobile Sheet -->
<Sheet.Root bind:open={sheetOpen}>
  <Sheet.Content side="right">
    <Sheet.Header>
      <Sheet.Title>
        {#if session && user}
          <div class="flex flex-col gap-1">
            <span class="text-sm leading-none font-medium"
              >{user.name ?? m.common_ui_user_fallback()}</span
            >
            {#if user.email}
              <span class="text-muted-foreground text-xs leading-none font-normal">
                {user.email}
              </span>
            {/if}
          </div>
        {:else}
          {m.webclient_nav_brand()}
        {/if}
      </Sheet.Title>
    </Sheet.Header>

    <div class="flex flex-col gap-4 px-4 py-4">
      {#if session && user}
        <Button
          variant="ghost"
          class="justify-start"
          onclick={() => (sheetOpen = false)}
          href={resolve("/account/profile")}
        >
          <UserRoundCogIcon class="mr-2 size-4" />
          {m.common_ui_account()}
        </Button>

        <div class="bg-border h-px"></div>
      {:else}
        <div class="flex flex-col gap-2">
          <Button
            variant="default"
            onclick={() => (sheetOpen = false)}
            href={resolve("/sign-up")}>{m.common_ui_sign_up()}</Button
          >
          <Button
            variant="outline"
            onclick={() => (sheetOpen = false)}
            href={resolve("/sign-in")}>{m.common_ui_sign_in()}</Button
          >
        </div>

        <div class="bg-border h-px"></div>
      {/if}

      <div>
        <p class="text-muted-foreground mb-1.5 text-xs font-medium">{m.common_ui_mode()}</p>
        <SegmentedControl
          segments={modeSegments}
          value={currentMode}
          onchange={handleModeChange}
          size="sm"
          class="w-full"
        />
      </div>

      <div>
        <p class="text-muted-foreground mb-1.5 text-xs font-medium">{m.common_ui_theme()}</p>
        <SegmentedControl
          segments={themeSegments}
          value={currentTheme}
          onchange={handleThemeChange}
          size="sm"
          class="w-full"
        />
      </div>

      <Button
        variant="ghost"
        class="justify-start"
        onclick={() => {
          languageModalOpen = true;
          sheetOpen = false;
        }}
      >
        <LanguagesIcon class="mr-2 size-4" />
        {currentLocaleLabel}
      </Button>

      <Button
        variant="ghost"
        class="justify-start"
        onclick={() => {
          timezoneModalOpen = true;
          sheetOpen = false;
        }}
      >
        <ClockIcon class="mr-2 size-4" />
        {currentTimezoneLabel}
      </Button>

      {#if session}
        <div class="bg-border h-px"></div>

        <Button
          variant="ghost"
          class="justify-start"
          onclick={() => {
            sheetOpen = false;
            handleSignOut();
          }}
          disabled={signingOut}
        >
          <LogOutIcon class="mr-2 size-4" />
          {m.common_ui_sign_out()}
        </Button>
      {/if}
    </div>
  </Sheet.Content>
</Sheet.Root>
