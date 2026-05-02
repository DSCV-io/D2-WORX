<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import LayoutDashboardIcon from "@lucide/svelte/icons/layout-dashboard";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import UserIcon from "@lucide/svelte/icons/user";
  import { resolve } from "$app/paths";
  import * as Sidebar from "$lib/client/components/ui/sidebar/index.js";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import * as m from "$lib/paraglide/messages.js";

  interface Props {
    orgType?: string;
    role?: string;
  }

  let { orgType = "customer", role = "owner" }: Props = $props();

  const navItems = $derived([
    { label: m.common_ui_dashboard(), href: "/dashboard" as const, icon: LayoutDashboardIcon },
    { label: m.webclient_app_settings_title(), href: "/settings" as const, icon: SettingsIcon },
    { label: m.webclient_app_profile_title(), href: "/account/profile" as const, icon: UserIcon },
  ]);
</script>

<Sidebar.Sidebar collapsible="icon">
  <Sidebar.SidebarHeader>
    <Sidebar.SidebarMenu>
      <Sidebar.SidebarMenuItem>
        <Sidebar.SidebarMenuButton
          size="lg"
          class="pointer-events-none"
        >
          <div
            class="bg-primary text-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg text-xs font-bold"
          >
            DW
          </div>
          <div class="flex flex-col gap-0.5 leading-none">
            <span class="font-semibold">{m.webclient_nav_brand()}</span>
            <span class="text-xs capitalize">{orgType}</span>
          </div>
        </Sidebar.SidebarMenuButton>
      </Sidebar.SidebarMenuItem>
    </Sidebar.SidebarMenu>
  </Sidebar.SidebarHeader>

  <Sidebar.SidebarContent>
    <Sidebar.SidebarGroup>
      <Sidebar.SidebarGroupLabel>{m.webclient_nav_navigation()}</Sidebar.SidebarGroupLabel>
      <Sidebar.SidebarGroupContent>
        <Sidebar.SidebarMenu>
          {#each navItems as item (item.href)}
            <Sidebar.SidebarMenuItem>
              <Sidebar.SidebarMenuButton tooltipContent={item.label}>
                {#snippet child({ props })}
                  <a
                    href={resolve(item.href)}
                    {...props}
                  >
                    <item.icon />
                    <span>{item.label}</span>
                  </a>
                {/snippet}
              </Sidebar.SidebarMenuButton>
            </Sidebar.SidebarMenuItem>
          {/each}
        </Sidebar.SidebarMenu>
      </Sidebar.SidebarGroupContent>
    </Sidebar.SidebarGroup>
  </Sidebar.SidebarContent>

  <Sidebar.SidebarFooter>
    <Sidebar.SidebarMenu>
      <Sidebar.SidebarMenuItem>
        <Sidebar.SidebarMenuButton
          size="lg"
          class="pointer-events-none"
        >
          <Avatar.Avatar class="size-8 rounded-lg">
            <Avatar.AvatarFallback class="rounded-lg">U</Avatar.AvatarFallback>
          </Avatar.Avatar>
          <div class="flex flex-col gap-0.5 leading-none">
            <!-- Static placeholder — real signed-in user data is rendered by UserAvatarMenu in the header. -->
            <span class="text-sm font-medium">{m.common_ui_user_fallback()}</span>
            <span class="text-muted-foreground text-xs">user@example.com</span>
          </div>
        </Sidebar.SidebarMenuButton>
      </Sidebar.SidebarMenuItem>
    </Sidebar.SidebarMenu>
  </Sidebar.SidebarFooter>

  <Sidebar.SidebarRail />
</Sidebar.Sidebar>

<style>
  /*
   * Peek/reverse-peek effect — Stripe-style sidebar edge animation.
   * Expanded + hover: border tucks in (recedes), signaling collapsible.
   * Collapsed + hover: border peeks out (extends), signaling expandable.
   * Uses the sidebar-inner's right border via a pseudo-element overlay.
   */
  :global([data-slot="sidebar"][data-side="left"] [data-slot="sidebar-container"]) {
    --peek-offset: 0.375rem;
  }

  :global([data-slot="sidebar"][data-side="left"] [data-slot="sidebar-inner"]) {
    position: relative;
  }

  /* Remove default right border — replaced by the ::after peek indicator */
  :global([data-slot="sidebar"][data-side="left"] [data-slot="sidebar-container"]) {
    border-inline-end: none;
  }

  :global([data-slot="sidebar"][data-side="left"] [data-slot="sidebar-inner"]::after) {
    content: "";
    position: absolute;
    top: 0;
    right: 0;
    bottom: 0;
    width: 2px;
    background: hsl(var(--sidebar-border));
    transition:
      transform 300ms ease,
      background-color 300ms ease,
      width 300ms ease;
    will-change: transform;
  }

  /* Expanded — default: flush right */
  :global([data-slot="sidebar"][data-state="expanded"] [data-slot="sidebar-inner"]::after) {
    transform: translateX(0);
  }

  /* Expanded — hover: edge recedes (tucks in) */
  :global([data-slot="sidebar"][data-state="expanded"]:hover [data-slot="sidebar-inner"]::after) {
    transform: translateX(calc(var(--peek-offset) * -1));
    background: hsl(var(--primary));
    width: 2px;
  }

  /* Collapsed — default: edge receded */
  :global([data-slot="sidebar"][data-state="collapsed"] [data-slot="sidebar-inner"]::after) {
    transform: translateX(calc(var(--peek-offset) * -1));
  }

  /* Collapsed — hover: edge peeks out (extends) */
  :global([data-slot="sidebar"][data-state="collapsed"]:hover [data-slot="sidebar-inner"]::after) {
    transform: translateX(0);
    background: hsl(var(--primary));
    width: 3px;
  }
</style>
