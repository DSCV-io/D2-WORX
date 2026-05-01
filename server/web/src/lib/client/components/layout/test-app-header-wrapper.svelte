<script lang="ts">
  import { SidebarProvider } from "$lib/client/components/ui/sidebar/index.js";
  import AppHeader from "./app-header.svelte";
</script>

<SidebarProvider open={true}>
  <AppHeader />
</SidebarProvider>
