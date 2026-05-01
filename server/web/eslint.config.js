import js from "@eslint/js";
import prettier from "eslint-config-prettier";
import svelte from "eslint-plugin-svelte";
import globals from "globals";
import ts from "typescript-eslint";
import { defineConfig, globalIgnores } from "eslint/config";

export default defineConfig(
  // Global ignores
  globalIgnores([
    "**/dist/",
    "**/build/",
    "**/.svelte-kit/",
    "**/node_modules/",
    "**/src/generated/",
    "old/",
    "**/bin/",
    "**/obj/",
    "clients/web/src/paraglide/",
    "clients/web/src/lib/paraglide/",
  ]),

  // JS recommended for all JS/TS
  {
    files: ["**/*.{js,mjs,cjs,ts,mts,cts}"],
    extends: [js.configs.recommended],
  },

  // TypeScript recommended
  {
    files: ["**/*.{ts,mts,cts}"],
    extends: [...ts.configs.recommended],
    rules: {
      "no-undef": "off",
      "@typescript-eslint/no-unused-vars": [
        "error",
        { argsIgnorePattern: "^_", varsIgnorePattern: "^_" },
      ],
      "@typescript-eslint/no-empty-object-type": ["error", { allowInterfaces: "always" }],
    },
  },

  // Backend Node.js globals
  {
    files: ["backends/node/**/*.{js,ts}"],
    languageOptions: {
      globals: { ...globals.node },
    },
  },

  // Web client globals
  {
    files: ["clients/web/**/*.{js,ts}"],
    languageOptions: {
      globals: { ...globals.browser, ...globals.node },
    },
  },

  // Svelte files
  ...svelte.configs.recommended.map((config) => ({
    ...config,
    files: ["clients/web/**/*.svelte", "clients/web/**/*.svelte.ts", "clients/web/**/*.svelte.js"],
  })),
  {
    files: ["clients/web/**/*.svelte", "clients/web/**/*.svelte.ts", "clients/web/**/*.svelte.js"],
    languageOptions: {
      parserOptions: {
        projectService: true,
        extraFileExtensions: [".svelte"],
        parser: ts.parser,
      },
    },
  },
  ...svelte.configs.prettier.map((config) => ({
    ...config,
    files: ["clients/web/**/*.svelte", "clients/web/**/*.svelte.ts", "clients/web/**/*.svelte.js"],
  })),

  // Test files — relaxed rules
  {
    files: [
      "**/*.test.{ts,js}",
      "**/*.spec.{ts,js}",
      "**/tests/**/*.{ts,js}",
      "**/testing/**/*.{ts,js}",
    ],
    rules: {
      "@typescript-eslint/no-explicit-any": "off",
      "@typescript-eslint/no-unused-vars": "off",
    },
  },

  // Root config files
  {
    files: ["*.config.{js,ts,mjs}", "*.shared.{js,ts}"],
    languageOptions: {
      globals: { ...globals.node },
    },
  },

  // Prettier — MUST be last
  prettier,
);
