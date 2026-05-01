<script lang="ts">
  import { SidebarProvider } from "$lib/client/components/ui/sidebar/index.js";
  import AppSidebar from "./app-sidebar.svelte";

  interface Props {
    orgType?: string;
    role?: string;
  }

  let { orgType = "customer", role = "owner" }: Props = $props();
</script>

<SidebarProvider open={true}>
  <AppSidebar
    {orgType}
    {role}
  />
</SidebarProvider>
