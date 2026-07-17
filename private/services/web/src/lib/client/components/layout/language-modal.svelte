<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import ConfirmationDialog from "$lib/client/components/ui/confirmation-dialog.svelte";
  import { getLocale } from "$lib/paraglide/runtime";
  import { page } from "$app/stores";
  import { changeLocale } from "$lib/client/utils/change-locale.js";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";
  import { cn } from "$lib/shared/utils/utils.js";
  import CheckIcon from "@lucide/svelte/icons/check";

  let {
    open = $bindable(false),
  }: {
    open?: boolean;
  } = $props();

  const locales: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );

  let selectedCode: string = $state(getLocale());
  let confirmOpen = $state(false);

  // Reset selection when modal opens
  $effect(() => {
    if (open) {
      selectedCode = getLocale();
    }
  });

  function requestApply() {
    if (selectedCode === getLocale()) {
      open = false;
      return;
    }
    confirmOpen = true;
  }

  async function applyConfirmed() {
    open = false;
    await changeLocale(selectedCode, !!$page.data.session);
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content class="max-w-sm">
    <Dialog.Header>
      <Dialog.Title>{m.common_ui_language()}</Dialog.Title>
      <Dialog.Description>{m.common_ui_choose_language()}</Dialog.Description>
    </Dialog.Header>

    <div class="flex flex-col gap-1 py-2">
      {#each locales as locale (locale.code)}
        <button
          type="button"
          onclick={() => (selectedCode = locale.code)}
          class={cn(
            "flex items-center gap-3 rounded-md px-3 py-2 text-sm transition-colors",
            selectedCode === locale.code
              ? "bg-muted text-foreground"
              : "text-muted-foreground hover:bg-muted/50 hover:text-foreground",
          )}
        >
          {#if locale.flag}
            <img
              src={locale.flag}
              alt=""
              class="h-3 w-4 shrink-0 object-cover"
            />
          {/if}
          <span class="flex-1 text-left">{locale.endonym}</span>
          {#if selectedCode === locale.code}
            <CheckIcon class="size-4 shrink-0" />
          {/if}
        </button>
      {/each}
    </div>

    <Dialog.Footer>
      <Button
        variant="ghost"
        onclick={() => (open = false)}>{m.common_ui_cancel()}</Button
      >
      <Button onclick={requestApply}>{m.common_ui_set_language()}</Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>

<ConfirmationDialog
  bind:open={confirmOpen}
  title={m.common_ui_change_language_title()}
  description={m.common_ui_change_language_description()}
  confirmLabel={m.common_ui_change_language_confirm()}
  onConfirm={applyConfirmed}
/>
