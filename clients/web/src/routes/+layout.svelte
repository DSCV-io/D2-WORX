<script lang="ts">
  import "../app.css";
  import favicon from "$lib/assets/favicon.svg";
  import { ModeWatcher } from "mode-watcher";
  import { Toaster } from "$lib/client/components/ui/sonner/index.js";
  import ThemeProvider from "$lib/client/components/theme-provider.svelte";
  import NavigationProgress from "$lib/client/components/layout/navigation-progress.svelte";
  import { invalidateAll } from "$app/navigation";
  import { setClientFingerprint, getToken } from "$lib/client/rest/gateway-client.js";
  import { bustSessionCache } from "$lib/client/rest/account-client.js";
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

  // Auto-set D2_TIMEZONE cookie from browser on first visit (if not already set).
  // Server reads this cookie for SSR and sign-up flows.
  //
  // Skip "UTC" + "Etc/*" — these are technical/fallback values (CI runners,
  // headless browsers, misconfigured systems) rather than user preferences,
  // and they aren't in Geo's `timezones` reference table (which only seeds
  // geographic IANA names: Africa/*, America/*, Asia/*, etc). Persisting one
  // would propagate into the Geo contact insert and trip the
  // FK_contacts_timezones_iana_identifier constraint. The server-side
  // default ("America/New_York") applies instead. Real UTC-region users can
  // set it explicitly via the timezone modal.
  $effect(() => {
    if (!document.cookie.includes("D2_TIMEZONE=")) {
      const tz = Intl.DateTimeFormat().resolvedOptions().timeZone;
      if (tz && tz !== "UTC" && !tz.startsWith("Etc/")) {
        document.cookie = `D2_TIMEZONE=${tz}; path=/; max-age=34560000; SameSite=Lax`;
      }
    }
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
          bustSessionCache()
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
