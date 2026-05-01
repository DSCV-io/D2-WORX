<script lang="ts">
  import Section from "./section.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import PlusIcon from "@lucide/svelte/icons/plus";
  import MailIcon from "@lucide/svelte/icons/mail";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";
  import SparklesIcon from "@lucide/svelte/icons/sparkles";

  const variants = [
    "default",
    "secondary",
    "accent",
    "destructive",
    "outline",
    "ghost",
    "link",
  ] as const;
  const sizes = ["sm", "default", "lg"] as const;
</script>

<Section
  id="buttons"
  title="Buttons"
>
  <div class="flex flex-col gap-6">
    <!-- Variant × Size grid -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Variants &times; Sizes</h3>
      <div class="overflow-x-auto rounded-lg border p-6">
        <div
          class="grid grid-cols-4 items-center gap-4"
          style="grid-template-columns: auto repeat(3, 1fr);"
        >
          <div></div>
          {#each sizes as size (size)}
            <div class="text-muted-foreground text-center text-xs font-medium">{size}</div>
          {/each}
          {#each variants as variant (variant)}
            <div class="text-muted-foreground pr-2 text-right text-xs font-medium">{variant}</div>
            {#each sizes as size (size)}
              <div class="flex justify-center">
                <Button
                  {variant}
                  {size}>{variant}</Button
                >
              </div>
            {/each}
          {/each}
        </div>
      </div>
    </div>

    <!-- Disabled -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Disabled States</h3>
      <div class="flex flex-wrap gap-3 rounded-lg border p-6">
        {#each variants as variant (variant)}
          <Button
            {variant}
            disabled>{variant}</Button
          >
        {/each}
      </div>
    </div>

    <!-- With icons -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">With Icons</h3>
      <div class="flex flex-wrap items-center gap-3 rounded-lg border p-6">
        <Button>
          <MailIcon class="size-4" />
          Send Email
        </Button>
        <Button variant="secondary">
          <PlusIcon class="size-4" />
          Add Item
        </Button>
        <Button variant="accent">
          <SparklesIcon class="size-4" />
          Try It
        </Button>
        <Button variant="outline">
          <LoaderCircleIcon class="size-4 animate-spin" />
          Loading...
        </Button>
        <Button
          variant="ghost"
          size="icon"
        >
          <PlusIcon class="size-4" />
        </Button>
        <Button
          variant="outline"
          size="icon-sm"
        >
          <MailIcon class="size-4" />
        </Button>
      </div>
    </div>
  </div>
</Section>
