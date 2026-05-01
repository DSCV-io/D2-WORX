<script lang="ts">
  import Section from "./section.svelte";
  import * as Resizable from "$lib/client/components/ui/resizable/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import SearchIcon from "@lucide/svelte/icons/search";
  import UploadIcon from "@lucide/svelte/icons/upload";
  import CheckIcon from "@lucide/svelte/icons/check";

  const steps = [
    { label: "Details", completed: true },
    { label: "Payment", completed: false, active: true },
    { label: "Confirmation", completed: false },
  ];
</script>

<Section
  id="layout"
  title="Layout & Patterns"
>
  <div class="flex flex-col gap-6">
    <!-- Resizable -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Resizable Panels</h3>
      <div class="rounded-lg border">
        <Resizable.PaneGroup
          direction="horizontal"
          class="min-h-[200px]"
        >
          <Resizable.Pane
            defaultSize={30}
            minSize={20}
          >
            <div class="flex h-full items-center justify-center p-4">
              <span class="text-muted-foreground text-sm font-medium">Sidebar</span>
            </div>
          </Resizable.Pane>
          <Resizable.Handle withHandle />
          <Resizable.Pane
            defaultSize={70}
            minSize={30}
          >
            <Resizable.PaneGroup direction="vertical">
              <Resizable.Pane
                defaultSize={60}
                minSize={20}
              >
                <div class="flex h-full items-center justify-center p-4">
                  <span class="text-muted-foreground text-sm font-medium">Content</span>
                </div>
              </Resizable.Pane>
              <Resizable.Handle withHandle />
              <Resizable.Pane
                defaultSize={40}
                minSize={20}
              >
                <div class="flex h-full items-center justify-center p-4">
                  <span class="text-muted-foreground text-sm font-medium">Footer</span>
                </div>
              </Resizable.Pane>
            </Resizable.PaneGroup>
          </Resizable.Pane>
        </Resizable.PaneGroup>
      </div>
    </div>

    <div class="grid gap-6 md:grid-cols-2">
      <!-- Button Group -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Button Group</h3>
        <div class="flex flex-col gap-4 rounded-lg border p-6">
          <div class="inline-flex rounded-md shadow-sm">
            <Button
              variant="outline"
              class="rounded-r-none border-r-0"
            >
              Previous
            </Button>
            <Button
              variant="outline"
              class="rounded-none border-r-0"
            >
              Current
            </Button>
            <Button
              variant="outline"
              class="rounded-l-none"
            >
              Next
            </Button>
          </div>
          <div class="inline-flex rounded-md shadow-sm">
            <Button
              size="sm"
              class="rounded-r-none">Save</Button
            >
            <Button
              size="sm"
              variant="secondary"
              class="rounded-none border-x-0">Draft</Button
            >
            <Button
              size="sm"
              variant="outline"
              class="rounded-l-none">Cancel</Button
            >
          </div>
        </div>
      </div>

      <!-- Search Input -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Search Input</h3>
        <div class="flex flex-col gap-4 rounded-lg border p-6">
          <div class="relative">
            <SearchIcon
              class="text-muted-foreground absolute top-1/2 left-3 size-4 -translate-y-1/2"
            />
            <Input
              placeholder="Search..."
              class="pl-9"
            />
          </div>
          <div class="relative">
            <SearchIcon
              class="text-muted-foreground absolute top-1/2 left-3 size-4 -translate-y-1/2"
            />
            <Input
              placeholder="Search..."
              class="pr-20 pl-9"
            />
            <kbd
              class="bg-muted text-muted-foreground pointer-events-none absolute top-1/2 right-3 -translate-y-1/2 rounded border px-1.5 py-0.5 text-[10px] font-medium"
            >
              Ctrl+K
            </kbd>
          </div>
        </div>
      </div>

      <!-- File Upload -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">File Upload Zone</h3>
        <div class="rounded-lg border p-6">
          <div
            class="hover:border-primary/50 hover:bg-muted/50 flex flex-col items-center justify-center gap-3 rounded-lg border-2 border-dashed py-10 transition-colors"
          >
            <div class="bg-muted rounded-full p-3">
              <UploadIcon class="text-muted-foreground size-6" />
            </div>
            <div class="text-center">
              <p class="text-sm font-medium">Drop files here or click to upload</p>
              <p class="text-muted-foreground mt-1 text-xs">PNG, JPG, GIF up to 10MB</p>
            </div>
            <Button
              variant="outline"
              size="sm">Browse Files</Button
            >
          </div>
        </div>
      </div>

      <!-- Stepper -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Stepper</h3>
        <div class="rounded-lg border p-6">
          <div class="flex items-center justify-between">
            {#each steps as step, i (step.label)}
              <div class="flex items-center gap-2">
                <div
                  class="flex size-8 items-center justify-center rounded-full border-2 text-sm font-medium transition-colors
                    {step.completed
                    ? 'border-primary bg-primary text-primary-foreground'
                    : step.active
                      ? 'border-primary text-primary'
                      : 'border-muted-foreground/30 text-muted-foreground'}"
                >
                  {#if step.completed}
                    <CheckIcon class="size-4" />
                  {:else}
                    {i + 1}
                  {/if}
                </div>
                <span
                  class="text-sm font-medium {step.completed || step.active
                    ? 'text-foreground'
                    : 'text-muted-foreground'}"
                >
                  {step.label}
                </span>
              </div>
              {#if i < steps.length - 1}
                <div
                  class="mx-2 h-0.5 flex-1 {step.completed
                    ? 'bg-primary'
                    : 'bg-muted-foreground/30'}"
                ></div>
              {/if}
            {/each}
          </div>
          <div class="mt-6 flex justify-between">
            <Button
              variant="outline"
              size="sm">Back</Button
            >
            <Button size="sm">Continue</Button>
          </div>
        </div>
      </div>
    </div>
  </div>
</Section>
