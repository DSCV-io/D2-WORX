<script lang="ts">
  import "../app.css";
  import favicon from "$lib/assets/favicon.svg";
  import { ModeWatcher } from "mode-watcher";
  import { Toaster } from "$lib/client/components/ui/sonner/index.js";
  import ThemeProvider from "$lib/client/components/theme-provider.svelte";
  import NavigationProgress from "$lib/client/components/layout/navigation-progress.svelte";
  import { invalidateAll } from "$app/navigation";
  import { setClientFingerprint, getToken } from "$lib/client/rest/gateway-client.js";
  import { generateClientFingerprint } from "$lib/client/utils/fingerprint.js";
  import { setFaroUser, resetFaroUser } from "$lib/client/telemetry/faro.js";
  import { createRealtimeClient, setRealtimeContext } from "$lib/client/realtime/index.js";
  import { env } from "$env/dynamic/public";
  import * as m from "$lib/paraglide/messages.js";

  let { data, children } = $props();

  // SignalR real-time client — created once, shared via context
  const realtimeClient = createRealtimeClient();
  setRealtimeContext(realtimeClient);

  $effect(() => {
    generateClientFingerprint().then((fp) => setClientFingerprint(fp));
  });

  // Enrich Faro telemetry with user ID + username for session correlation (no PII).
  $effect(() => {
    if (data.user) {
      setFaroUser(data.user.id, data.user.username);
    } else {
      resetFaroUser();
    }
  });

  // Connect SignalR when authenticated, disconnect on sign-out.
  // Also register the user:updated listener for session refresh.
  let userUpdatedUnsub: (() => void) | undefined;

  $effect(() => {
    if (data.session && env.PUBLIC_SIGNALR_URL) {
      realtimeClient.connect(
        `${env.PUBLIC_SIGNALR_URL}/hub/authenticated`,
        async () => (await getToken()) ?? "",
      );

      // Register user:updated listener (idempotent — only if not already registered)
      if (!userUpdatedUnsub) {
        userUpdatedUnsub = realtimeClient.on("user:updated", () => {
          console.debug("[user:updated] Refreshing session...");
          fetch("/api/auth/get-session?disableCookieCache=true", {
            method: "GET",
            credentials: "include",
          })
            .then(() => invalidateAll())
            .then(() => console.debug("[user:updated] Session refreshed"))
            .catch((err) => console.error("[user:updated] Failed:", err));
        });
      }
    } else {
      if (userUpdatedUnsub) {
        userUpdatedUnsub();
        userUpdatedUnsub = undefined;
      }
      realtimeClient.disconnect();
    }
  });
</script>

<svelte:head>
  <link
    rel="icon"
    href={favicon}
  />
  <title>{m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_hero_tagline()}
  />
</svelte:head>

<ModeWatcher />
<ThemeProvider />
<Toaster />
<NavigationProgress />

{@render children?.()}
