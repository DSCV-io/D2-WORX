<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import type { Snippet } from "svelte";
  import { resolve } from "$app/paths";
  import UserAvatarMenu from "$lib/client/components/layout/user-avatar-menu.svelte";
  import SettingsNav from "$lib/client/components/account/settings-nav.svelte";
  import { toast } from "svelte-sonner";
  import * as m from "$lib/paraglide/messages.js";
  import UserIcon from "@lucide/svelte/icons/user";
  import MailIcon from "@lucide/svelte/icons/mail";
  import ShieldIcon from "@lucide/svelte/icons/shield";
  import TrashIcon from "@lucide/svelte/icons/trash-2";

  let { children }: { children: Snippet } = $props();

  const mockUser = {
    id: "01234567-89ab-cdef-0123-456789abcdef",
    name: "John Doe",
    email: "john.doe@example.com",
  };

  const navItems = $derived([
    {
      href: resolve("/debug/design/account-components/profile"),
      label: m.webclient_app_account_profile_title(),
      icon: UserIcon,
    },
    {
      href: resolve("/debug/design/account-components/email-phone"),
      label: m.webclient_app_account_email_phone_title(),
      icon: MailIcon,
    },
    {
      href: resolve("/debug/design/account-components/security"),
      label: m.webclient_app_account_security_title(),
      icon: ShieldIcon,
    },
    {
      href: resolve("/debug/design/account-components/deactivate"),
      label: m.webclient_app_account_deactivate_title(),
      icon: TrashIcon,
    },
  ]);

  async function mockSignOut() {
    toast.info(m.webclient_debug_account_components_signout_clicked());
  }
</script>

<svelte:head>
  <title>{m.webclient_debug_account_components_title()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_debug_account_components_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="mx-auto max-w-5xl px-4 py-8">
  <!-- Header with avatar menu (mode, theme, language controls are inside the dropdown) -->
  <div class="mb-8 flex items-center justify-between">
    <div>
      <h1 class="text-3xl font-bold tracking-tight">
        {m.webclient_debug_account_components_title()}
      </h1>
      <p class="text-muted-foreground mt-1 text-sm">
        {m.webclient_debug_account_components_description()}
      </p>
    </div>
    <UserAvatarMenu
      user={mockUser}
      onSignOut={mockSignOut}
      size="lg"
    />
  </div>

  <!-- Two-column layout: settings nav + routed content -->
  <div class="flex flex-col gap-6 md:flex-row md:gap-8">
    <SettingsNav items={navItems} />

    <div class="min-w-0 flex-1">
      {@render children()}
    </div>
  </div>

  <div class="h-16"></div>
</div>
