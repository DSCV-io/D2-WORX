<script lang="ts">
  import Section from "./section.svelte";
  import { Progress } from "$lib/client/components/ui/progress/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { toast } from "svelte-sonner";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";
  import InboxIcon from "@lucide/svelte/icons/inbox";
</script>

<Section
  id="feedback"
  title="Feedback"
>
  <div class="grid gap-6 md:grid-cols-2">
    <!-- Progress -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Progress Bars</h3>
      <div class="flex flex-col gap-3">
        <div class="flex flex-col gap-1.5">
          <span class="text-muted-foreground text-xs">25%</span>
          <Progress value={25} />
        </div>
        <div class="flex flex-col gap-1.5">
          <span class="text-muted-foreground text-xs">50%</span>
          <Progress value={50} />
        </div>
        <div class="flex flex-col gap-1.5">
          <span class="text-muted-foreground text-xs">75%</span>
          <Progress value={75} />
        </div>
        <div class="flex flex-col gap-1.5">
          <span class="text-muted-foreground text-xs">100%</span>
          <Progress value={100} />
        </div>
      </div>
    </div>

    <!-- Skeleton -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Skeleton Loaders</h3>
      <div class="flex flex-col gap-4">
        <!-- Card skeleton -->
        <div class="flex items-center gap-4">
          <Skeleton class="size-12 rounded-full" />
          <div class="flex flex-col gap-2">
            <Skeleton class="h-4 w-48" />
            <Skeleton class="h-3 w-32" />
          </div>
        </div>
        <!-- Content skeleton -->
        <div class="flex flex-col gap-2">
          <Skeleton class="h-4 w-full" />
          <Skeleton class="h-4 w-4/5" />
          <Skeleton class="h-4 w-3/5" />
        </div>
        <!-- Button skeleton -->
        <div class="flex gap-2">
          <Skeleton class="h-9 w-24" />
          <Skeleton class="h-9 w-24" />
        </div>
      </div>
    </div>

    <!-- Spinner -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Spinners</h3>
      <div class="flex items-center gap-8">
        <div class="flex flex-col items-center gap-2">
          <LoaderCircleIcon class="text-muted-foreground size-4 animate-spin" />
          <span class="text-muted-foreground text-xs">sm</span>
        </div>
        <div class="flex flex-col items-center gap-2">
          <LoaderCircleIcon class="text-muted-foreground size-6 animate-spin" />
          <span class="text-muted-foreground text-xs">md</span>
        </div>
        <div class="flex flex-col items-center gap-2">
          <LoaderCircleIcon class="text-muted-foreground size-10 animate-spin" />
          <span class="text-muted-foreground text-xs">lg</span>
        </div>
        <div class="flex flex-col items-center gap-2">
          <LoaderCircleIcon class="text-primary size-6 animate-spin" />
          <span class="text-muted-foreground text-xs">primary</span>
        </div>
      </div>
    </div>

    <!-- Empty State -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Empty State</h3>
      <div class="flex flex-col items-center justify-center gap-3 py-8">
        <div class="bg-muted rounded-full p-3">
          <InboxIcon class="text-muted-foreground size-8" />
        </div>
        <div class="text-center">
          <h4 class="text-sm font-medium">No results found</h4>
          <p class="text-muted-foreground mt-1 text-xs">
            Try adjusting your search or filter to find what you're looking for.
          </p>
        </div>
        <Button
          variant="outline"
          size="sm">Clear Filters</Button
        >
      </div>
    </div>

    <!-- Toasts -->
    <div class="flex flex-col gap-4 rounded-lg border p-6 md:col-span-2">
      <h3 class="text-muted-foreground text-sm font-medium">Toast Notifications</h3>
      <div class="flex flex-wrap gap-3">
        <Button
          variant="outline"
          onclick={() => toast("Event has been created")}
        >
          Default Toast
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.success("Successfully saved!")}
        >
          Success Toast
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.error("Something went wrong")}
        >
          Error Toast
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.warning("Please review before continuing")}
        >
          Warning Toast
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.info("New version available")}
        >
          Info Toast
        </Button>
        <Button
          variant="outline"
          onclick={() => toast("Event created", { description: "Monday, January 3rd at 6:00pm" })}
        >
          With Description
        </Button>
      </div>
    </div>
  </div>
</Section>
