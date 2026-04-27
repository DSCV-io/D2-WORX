<script lang="ts">
  import ThemeToggle from "$lib/client/components/theme-toggle.svelte";
  import ThemeSelector from "$lib/client/components/theme-selector.svelte";
  import ThemeEditor from "$lib/client/components/design/theme-editor.svelte";
  import ColorPalette from "$lib/client/components/design/color-palette.svelte";
  import TypographyShowcase from "$lib/client/components/design/typography-showcase.svelte";
  import ButtonShowcase from "$lib/client/components/design/button-showcase.svelte";
  import CardShowcase from "$lib/client/components/design/card-showcase.svelte";
  import FormShowcase from "$lib/client/components/design/form-showcase.svelte";
  import OverlayShowcase from "$lib/client/components/design/overlay-showcase.svelte";
  import NavigationShowcase from "$lib/client/components/design/navigation-showcase.svelte";
  import FeedbackShowcase from "$lib/client/components/design/feedback-showcase.svelte";
  import DataDisplayShowcase from "$lib/client/components/design/data-display-showcase.svelte";
  import ChartShowcase from "$lib/client/components/design/chart-showcase.svelte";
  import LayoutShowcase from "$lib/client/components/design/layout-showcase.svelte";
  import { resolve } from "$app/paths";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import PaletteIcon from "@lucide/svelte/icons/palette";
  import * as m from "$lib/paraglide/messages.js";

  let editorOpen = $state(false);

  const sections = $derived([
    { id: "colors", label: m.webclient_design_section_colors() },
    { id: "typography", label: m.webclient_design_section_typography() },
    { id: "buttons", label: m.webclient_design_section_buttons() },
    { id: "cards", label: m.webclient_design_section_cards() },
    { id: "forms", label: m.webclient_design_section_forms() },
    { id: "overlays", label: m.webclient_design_section_overlays() },
    { id: "navigation", label: m.webclient_design_section_navigation() },
    { id: "feedback", label: m.webclient_design_section_feedback() },
    { id: "data-display", label: m.webclient_design_section_data_display() },
    { id: "charts", label: m.webclient_design_section_charts() },
    { id: "layout", label: m.webclient_design_section_layout() },
  ]);

  const demos = $derived([
    {
      href: "/debug/design/contact-form" as const,
      label: m.webclient_design_demo_contact_form(),
      description: m.webclient_design_demo_contact_form_description(),
    },
    {
      href: "/debug/design/signup-form" as const,
      label: m.webclient_design_demo_signup_form(),
      description: m.webclient_design_demo_signup_form_description(),
    },
    {
      href: "/debug/design/account-components" as const,
      label: m.webclient_debug_account_components_title(),
      description: m.webclient_debug_account_components_subtitle(),
    },
  ]);
</script>

<svelte:head>
  <title>{m.common_ui_design_system()} — {m.webclient_nav_brand()}</title>
  <meta
    name="description"
    content={m.webclient_design_description()}
  />
  <meta
    name="robots"
    content="noindex, nofollow"
  />
  <meta
    property="og:title"
    content="{m.common_ui_design_system()} — {m.webclient_nav_brand()}"
  />
  <meta
    property="og:description"
    content={m.webclient_design_description()}
  />
  <meta
    property="og:type"
    content="website"
  />
</svelte:head>

<div class="mx-auto max-w-6xl px-4 py-8">
  <!-- Header -->
  <div class="mb-8 flex items-center justify-between">
    <div>
      <h1 class="text-3xl font-bold tracking-tight">{m.common_ui_design_system()}</h1>
      <p class="text-muted-foreground mt-1">
        {m.webclient_design_kitchen_sink()}
      </p>
    </div>
    <div class="flex items-center gap-2">
      <ThemeSelector />
      <ThemeToggle />
      <Button
        variant="outline"
        onclick={() => (editorOpen = true)}
      >
        <PaletteIcon class="size-4" />
        {m.webclient_design_theme_editor()}
      </Button>
    </div>
  </div>

  <!-- Section nav -->
  <nav
    class="bg-background/95 supports-[backdrop-filter]:bg-background/60 sticky top-0 z-10 -mx-4 mb-8 overflow-x-auto border-b px-4 py-3 backdrop-blur"
  >
    <div class="flex gap-2">
      {#each sections as s (s.id)}
        <a
          href="#{s.id}"
          class="text-muted-foreground hover:bg-accent hover:text-accent-foreground rounded-md px-3 py-1.5 text-sm font-medium transition-colors"
        >
          {s.label}
        </a>
      {/each}
    </div>
  </nav>

  <!-- Demo pages -->
  {#if demos.length > 0}
    <div class="mb-8 flex flex-wrap gap-3">
      {#each demos as demo (demo.href)}
        <a
          href={resolve(demo.href as "/debug/design/contact-form")}
          class="group hover:border-primary hover:bg-accent rounded-lg border p-4 transition-colors"
        >
          <span class="group-hover:text-primary font-medium">{demo.label}</span>
          <p class="text-muted-foreground mt-1 text-xs">{demo.description}</p>
        </a>
      {/each}
    </div>
  {/if}

  <!-- Showcase sections -->
  <div class="flex flex-col gap-16">
    <ColorPalette />
    <TypographyShowcase />
    <ButtonShowcase />
    <CardShowcase />
    <FormShowcase />
    <OverlayShowcase />
    <NavigationShowcase />
    <FeedbackShowcase />
    <DataDisplayShowcase />
    <ChartShowcase />
    <LayoutShowcase />
  </div>
</div>

<ThemeEditor bind:open={editorOpen} />
