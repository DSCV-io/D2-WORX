<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import Section from "./section.svelte";
  import { Progress } from "$lib/client/components/ui/progress/index.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { toast } from "svelte-sonner";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";
  import InboxIcon from "@lucide/svelte/icons/inbox";
  import * as m from "$lib/paraglide/messages.js";
</script>

<Section
  id="feedback"
  title={m.webclient_design_section_feedback()}
>
  <div class="grid gap-6 md:grid-cols-2">
    <!-- Progress -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">
        {m.webclient_design_feedback_progress_bars()}
      </h3>
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
      <h3 class="text-muted-foreground text-sm font-medium">
        {m.webclient_design_feedback_skeleton_loaders()}
      </h3>
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
      <h3 class="text-muted-foreground text-sm font-medium">
        {m.webclient_design_feedback_spinners()}
      </h3>
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
      <h3 class="text-muted-foreground text-sm font-medium">
        {m.webclient_design_feedback_empty_state()}
      </h3>
      <div class="flex flex-col items-center justify-center gap-3 py-8">
        <div class="bg-muted rounded-full p-3">
          <InboxIcon class="text-muted-foreground size-8" />
        </div>
        <div class="text-center">
          <h4 class="text-sm font-medium">{m.webclient_design_feedback_no_results()}</h4>
          <p class="text-muted-foreground mt-1 text-xs">
            {m.webclient_design_feedback_no_results_hint()}
          </p>
        </div>
        <Button
          variant="outline"
          size="sm">{m.webclient_design_feedback_clear_filters()}</Button
        >
      </div>
    </div>

    <!-- Toasts -->
    <div class="flex flex-col gap-4 rounded-lg border p-6 md:col-span-2">
      <h3 class="text-muted-foreground text-sm font-medium">
        {m.webclient_design_feedback_toast_notifications()}
      </h3>
      <div class="flex flex-wrap gap-3">
        <Button
          variant="outline"
          onclick={() => toast(m.webclient_design_feedback_event_created())}
        >
          {m.webclient_design_feedback_toast_default()}
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.success(m.webclient_design_feedback_saved())}
        >
          {m.webclient_design_feedback_toast_success()}
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.error(m.webclient_error_something_went_wrong())}
        >
          {m.webclient_design_feedback_toast_error()}
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.warning(m.webclient_design_feedback_review_before_continuing())}
        >
          {m.webclient_design_feedback_toast_warning()}
        </Button>
        <Button
          variant="outline"
          onclick={() => toast.info(m.webclient_design_feedback_new_version())}
        >
          {m.webclient_design_feedback_toast_info()}
        </Button>
        <Button
          variant="outline"
          onclick={() =>
            toast(m.webclient_design_feedback_event_with_desc(), {
              description: m.webclient_design_feedback_event_date(),
            })}
        >
          {m.webclient_design_feedback_toast_with_description()}
        </Button>
      </div>
    </div>
  </div>
</Section>
