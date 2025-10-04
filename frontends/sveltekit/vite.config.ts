import { paraglideVitePlugin } from '@inlang/paraglide-js';
import tailwindcss from '@tailwindcss/vite';
import { defineConfig } from 'vitest/config';
import { sveltekit } from '@sveltejs/kit/vite';
import { loadEnv } from 'vite';

export default defineConfig(({mode}) => {
  const env = loadEnv(mode, process.cwd());
  const allowedHosts = safeParseArray(env.VITE_ALLOWED_HOSTS || '');

  return {
    server: {
      allowedHosts: allowedHosts,
    },
    plugins: [
      tailwindcss(),
      sveltekit(),
      paraglideVitePlugin({
        project: './project.inlang',
        outdir: './src/lib/paraglide'
      })
    ],
    test: {
      expect: { requireAssertions: true },
      projects: [
        {
          extends: './vite.config.ts',
          test: {
            name: 'client',
            environment: 'browser',
            browser: {
              enabled: true,
              provider: 'playwright',
              instances: [{ browser: 'chromium' }]
            },
            include: ['src/**/*.svelte.{test,spec}.{js,ts}'],
            exclude: ['src/lib/server/**'],
            setupFiles: ['./vitest-setup-client.ts']
          }
        },
        {
          extends: './vite.config.ts',
          test: {
            name: 'server',
            environment: 'node',
            include: ['src/**/*.{test,spec}.{js,ts}'],
            exclude: ['src/**/*.svelte.{test,spec}.{js,ts}']
          }
        }
      ]
    }
  }
});

function safeParseArray(v: string): string[] {
  try {
    const split = v.split(',');
    return split.map(item => item.trim());
  } catch {
    return [];
  }
}
