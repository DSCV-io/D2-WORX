<script lang="ts">
  import type { Snippet } from "svelte";
  import { resolve } from "$app/paths";
  import PublicNav from "$lib/client/components/layout/public-nav.svelte";
  import SettingsNav from "$lib/client/components/account/settings-nav.svelte";
  import * as m from "$lib/paraglide/messages.js";
  import UserIcon from "@lucide/svelte/icons/user";
  import MailIcon from "@lucide/svelte/icons/mail";
  import ShieldIcon from "@lucide/svelte/icons/shield";
  let { children }: { children: Snippet } = $props();

  const navItems = [
    {
      href: resolve("/account/profile"),
      label: m.webclient_app_account_profile_title(),
      icon: UserIcon,
    },
    {
      href: resolve("/account/email-phone"),
      label: m.webclient_app_account_email_phone_title(),
      icon: MailIcon,
    },
    {
      href: resolve("/account/security"),
      label: m.webclient_app_account_security_title(),
      icon: ShieldIcon,
    },
  ];
</script>

<svelte:head>
  <title>{m.webclient_app_account_page_title()}</title>
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="flex min-h-screen flex-col">
  <PublicNav />

  <div class="mx-auto w-full max-w-5xl flex-1 px-4 py-8">
    <div class="flex flex-col gap-6 md:flex-row md:gap-8">
      <SettingsNav items={navItems} />

      <!-- Cap form/content width so wide monitors don't stretch single-line
           inputs across the full viewport. Mirrors Stripe / Linear /
           Vercel — content stays scannable, whitespace fills the rest. -->
      <div class="max-w-2xl min-w-0 flex-1">
        {@render children()}
      </div>
    </div>

    <div class="h-16"></div>
  </div>
</div>
