<script lang="ts">
  import type { Snippet } from "svelte";
  import UserAvatarMenu from "$lib/client/components/layout/user-avatar-menu.svelte";
  import SettingsNav from "$lib/client/components/account/settings-nav.svelte";
  import { toast } from "svelte-sonner";
  import UserIcon from "@lucide/svelte/icons/user";
  import MailIcon from "@lucide/svelte/icons/mail";
  import ShieldIcon from "@lucide/svelte/icons/shield";
  import MonitorSmartphoneIcon from "@lucide/svelte/icons/monitor-smartphone";
  import ClockIcon from "@lucide/svelte/icons/clock";
  import TrashIcon from "@lucide/svelte/icons/trash-2";

  let { children }: { children: Snippet } = $props();

  const mockUser = {
    id: "01234567-89ab-cdef-0123-456789abcdef",
    name: "John Doe",
    email: "john.doe@example.com",
  };

  const navItems = [
    { href: "/debug/design/account-components/profile", label: "Profile", icon: UserIcon },
    {
      href: "/debug/design/account-components/email-phone",
      label: "Email & Phone",
      icon: MailIcon,
    },
    { href: "/debug/design/account-components/security", label: "Security", icon: ShieldIcon },
    {
      href: "/debug/design/account-components/sessions",
      label: "Sessions",
      icon: MonitorSmartphoneIcon,
    },
    {
      href: "/debug/design/account-components/recent-logins",
      label: "Recent Logins",
      icon: ClockIcon,
    },
    {
      href: "/debug/design/account-components/deactivate",
      label: "Deactivate Account",
      icon: TrashIcon,
    },
  ];

  async function mockSignOut() {
    toast.info("Sign out clicked");
  }
</script>

<svelte:head>
  <title>Account Components — Debug</title>
  <meta
    name="robots"
    content="noindex, nofollow"
  />
</svelte:head>

<div class="mx-auto max-w-5xl px-4 py-8">
  <!-- Header with avatar menu (mode, theme, language controls are inside the dropdown) -->
  <div class="mb-8 flex items-center justify-between">
    <div>
      <h1 class="text-3xl font-bold tracking-tight">Account Components</h1>
      <p class="text-muted-foreground mt-1 text-sm">
        Interactive demo — click the avatar for mode, theme, and language controls.
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
