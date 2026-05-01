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

  let { open = $bindable(false) }: { open?: boolean } = $props();
  let copied = $state(false);

  async function copyToClipboard() {
    try {
      await navigator.clipboard.writeText(getThemeCSS());
      copied = true;
      toast.success("Theme CSS copied to clipboard");
      setTimeout(() => (copied = false), 2000);
    } catch {
      toast.error("Failed to copy — try selecting and copying manually");
    }
  }
</script>

<Dialog bind:open>
  <DialogContent class="max-w-2xl">
    <DialogHeader>
      <DialogTitle>Generated Theme CSS</DialogTitle>
      <DialogDescription>
        Copy this CSS and replace the <code class="text-xs">@theme inline</code> and
        <code class="text-xs">.dark</code> blocks in
        <code class="text-xs">src/app.css</code>.
      </DialogDescription>
    </DialogHeader>

    <ScrollArea class="h-96 rounded-md border">
      <pre class="p-4 text-xs leading-relaxed"><code>{getThemeCSS()}</code></pre>
    </ScrollArea>

    <div class="flex justify-end">
      <Button onclick={copyToClipboard}>
        {#if copied}
          <CheckIcon class="size-4" />
          Copied
        {:else}
          <CopyIcon class="size-4" />
          Copy to Clipboard
        {/if}
      </Button>
    </div>
  </DialogContent>
</Dialog>
