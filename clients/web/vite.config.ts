import { paraglideVitePlugin } from "@inlang/paraglide-js";
import tailwindcss from "@tailwindcss/vite";
import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";
import { sveltekit } from "@sveltejs/kit/vite";
import { loadEnv } from "vite";

export default defineConfig(({ mode }) => {
  // Load env from monorepo root so .env.local vars (REDIS_URL, etc.) are
  // available in process.env during SSR and server-side hooks.
  const envDir = "../../";
  const env = loadEnv(mode, envDir);
  const allowedHosts = safeParseArray(env.VITE_ALLOWED_HOSTS || "");

  return {
    envDir,
    server: {
      allowedHosts: allowedHosts,
      watch: {
        // Docker bind mounts on Windows: native fs events don't propagate.
        // Polling required. Exclude generated/output dirs to prevent
        // infinite rebuild loops (.svelte-kit regenerates on watch events).
        usePolling: true,
        interval: 2000,
        ignored: ["**/.svelte-kit/**", "**/build/**", "**/screenshots/**"],
      },
      hmr: {
        // Cloudflare Tunnel: browser connects over wss://443, tunnel proxies
        // the WebSocket upgrade to the Vite dev server inside Docker.
        clientPort: 443,
        protocol: "wss",
      },
    },
    optimizeDeps: {
      include: [
        "bits-ui",
        "mode-watcher",
        "svelte-sonner",
        "tailwind-variants",
        "tailwind-merge",
        "clsx",
      ],
    },
    plugins: [
      tailwindcss(),
      sveltekit(),
      paraglideVitePlugin({
        project: "./project.inlang",
        outdir: "./src/lib/paraglide",
      }),
    ],
    test: {
      expect: { requireAssertions: true },
      projects: [
        {
          extends: "./vite.config.ts",
          test: {
            name: "client",
            browser: {
              enabled: true,
              provider: playwright(),
              instances: [{ browser: "chromium" }],
            },
            include: ["src/**/*.svelte.{test,spec}.{js,ts}"],
            exclude: ["src/lib/server/**"],
            setupFiles: ["./vitest-setup-client.ts"],
          },
        },
        {
          extends: "./vite.config.ts",
          test: {
            name: "server",
            environment: "node",
            include: ["src/**/*.{test,spec}.{js,ts}"],
            exclude: ["src/**/*.svelte.{test,spec}.{js,ts}"],
            server: {
              deps: {
                inline: [/sveltekit-superforms/],
              },
            },
          },
        },
      ],
    },
  };
});

function safeParseArray(v: string): string[] {
  try {
    const split = v.split(",");
    return split.map((item) => item.trim());
  } catch {
    return [];
  }
}
