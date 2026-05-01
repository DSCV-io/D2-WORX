<script lang="ts">
  import Section from "./section.svelte";
</script>

<Section
  id="typography"
  title="Typography"
>
  <div class="flex flex-col gap-6">
    <!-- Heading scale -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Heading Scale</h3>
      <div class="flex flex-col gap-2 rounded-lg border p-6">
        <h1 class="text-4xl font-bold tracking-tight">Heading 1 — The quick brown fox</h1>
        <h2 class="text-3xl font-semibold tracking-tight">Heading 2 — The quick brown fox</h2>
        <h3 class="text-2xl font-semibold tracking-tight">Heading 3 — The quick brown fox</h3>
        <h4 class="text-xl font-semibold">Heading 4 — The quick brown fox</h4>
        <h5 class="text-lg font-medium">Heading 5 — The quick brown fox</h5>
        <h6 class="text-base font-medium">Heading 6 — The quick brown fox</h6>
      </div>
    </div>

    <!-- Body text -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Body Text</h3>
      <div class="flex flex-col gap-3 rounded-lg border p-6">
        <p class="text-base leading-7">
          Body (base) — The quick brown fox jumps over the lazy dog. This is a standard body
          paragraph using the default text size. Gabarito provides excellent readability at all
          weights and sizes.
        </p>
        <p class="text-muted-foreground text-sm">
          Small / Muted — The quick brown fox jumps over the lazy dog. Used for secondary
          information, captions, and helper text throughout the interface.
        </p>
        <p class="text-muted-foreground text-xs">
          Extra Small — Used for labels, badges, and fine print. Still legible at this size.
        </p>
      </div>
    </div>

    <!-- Font weights -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Font Weights (Gabarito)</h3>
      <div class="flex flex-col gap-2 rounded-lg border p-6 text-lg">
        <p style="font-weight: 400">400 Regular — The quick brown fox</p>
        <p style="font-weight: 500">500 Medium — The quick brown fox</p>
        <p style="font-weight: 600">600 Semibold — The quick brown fox</p>
        <p style="font-weight: 700">700 Bold — The quick brown fox</p>
        <p style="font-weight: 800">800 Extrabold — The quick brown fox</p>
        <p style="font-weight: 900">900 Black — The quick brown fox</p>
      </div>
    </div>

    <!-- Inline elements -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Inline Elements</h3>
      <div class="flex flex-col gap-2 rounded-lg border p-6">
        <p>
          This has <strong>bold text</strong> and <em>italic text</em> and
          <code class="bg-muted rounded px-1.5 py-0.5 font-mono text-sm">inline code</code>.
        </p>
        <p>
          Here is a <a
            href="#typography"
            class="text-primary hover:text-primary/80 underline underline-offset-4">link style</a
          > in a sentence.
        </p>
      </div>
    </div>
  </div>
</Section>
