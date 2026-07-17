<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import {
    Dialog,
    DialogContent,
    DialogHeader,
    DialogTitle,
    DialogDescription,
  } from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { ScrollArea } from "$lib/client/components/ui/scroll-area/index.js";
  import { getThemeCSS } from "./theme-state.svelte.js";
  import { toast } from "svelte-sonner";
  import CopyIcon from "@lucide/svelte/icons/copy";
  import CheckIcon from "@lucide/svelte/icons/check";
  import * as m from "$lib/paraglide/messages.js";

  let { open = $bindable(false) }: { open?: boolean } = $props();
  let copied = $state(false);

  async function copyToClipboard() {
    try {
      await navigator.clipboard.writeText(getThemeCSS());
      copied = true;
      toast.success(m.webclient_design_export_dialog_toast_success());
      setTimeout(() => (copied = false), 2000);
    } catch {
      toast.error(m.webclient_design_export_dialog_toast_error());
    }
  }
</script>

<Dialog bind:open>
  <DialogContent class="max-w-2xl">
    <DialogHeader>
      <DialogTitle>{m.webclient_design_export_dialog_title()}</DialogTitle>
      <DialogDescription>
        {m.webclient_design_export_dialog_description_part1()}<code>@theme inline</code
        >{m.webclient_design_export_dialog_description_part2()}<code>.dark</code
        >{m.webclient_design_export_dialog_description_part3()}<code>src/app.css</code
        >{m.webclient_design_export_dialog_description_part4()}
      </DialogDescription>
    </DialogHeader>

    <ScrollArea class="h-96 rounded-md border">
      <pre class="p-4 text-xs leading-relaxed"><code>{getThemeCSS()}</code></pre>
    </ScrollArea>

    <div class="flex justify-end">
      <Button onclick={copyToClipboard}>
        {#if copied}
          <CheckIcon class="size-4" />
          {m.webclient_design_export_dialog_copied()}
        {:else}
          <CopyIcon class="size-4" />
          {m.webclient_design_export_dialog_copy()}
        {/if}
      </Button>
    </div>
  </DialogContent>
</Dialog>
