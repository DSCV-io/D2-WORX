<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Combobox } from "bits-ui";
  import { page } from "$app/stores";
  import { changeTimezone } from "$lib/client/utils/change-timezone.js";
  import { toast } from "svelte-sonner";
  import type { TimezoneOption } from "$lib/shared/forms/timezone-options.js";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import ChevronUpIcon from "@lucide/svelte/icons/chevron-up";
  import ChevronDownIcon from "@lucide/svelte/icons/chevron-down";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  let {
    open = $bindable(false),
  }: {
    open?: boolean;
  } = $props();

  const timezones: TimezoneOption[] = $derived(
    ($page.data as { timezoneOptions?: TimezoneOption[] }).timezoneOptions ?? [],
  );

  const currentTimezone = $derived(
    ($page.data.user as { timezone?: string } | null)?.timezone ??
      Intl.DateTimeFormat().resolvedOptions().timeZone,
  );

  let selectedValue: string = $state("");
  let searchValue = $state("");
  let comboboxOpen = $state(false);
  let saving = $state(false);

  $effect(() => {
    if (open) {
      selectedValue = currentTimezone;
      searchValue = "";
    }
  });

  const filteredOptions = $derived(
    searchValue
      ? timezones.filter((t) => t.label.toLowerCase().includes(searchValue.toLowerCase()))
      : timezones,
  );

  const selectedLabel = $derived(
    timezones.find((t) => t.value === selectedValue)?.label ?? selectedValue,
  );

  const isDirty = $derived(selectedValue !== currentTimezone);

  async function apply() {
    if (!isDirty) {
      open = false;
      return;
    }
    saving = true;
    try {
      await changeTimezone(selectedValue, !!$page.data.session);
      toast.success(m.common_ui_changes_saved());
      open = false;
    } catch {
      toast.error(m.common_errors_unknown());
    } finally {
      saving = false;
    }
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content class="max-w-sm">
    <Dialog.Header>
      <Dialog.Title>{m.account_profile_timezone()}</Dialog.Title>
      <Dialog.Description>{m.account_profile_timezone_description()}</Dialog.Description>
    </Dialog.Header>

    <div class="py-2">
      <Combobox.Root
        type="single"
        bind:open={comboboxOpen}
        value={selectedValue}
        onValueChange={(v) => {
          selectedValue = v;
          searchValue = "";
        }}
        items={timezones.map((t) => ({ value: t.value, label: t.label }))}
        onOpenChangeComplete={(isOpen) => {
          if (!isOpen) searchValue = "";
        }}
      >
        <div class="relative">
          <Combobox.Input
            autocomplete="off"
            value={comboboxOpen ? searchValue : selectedLabel}
            oninput={(e) => {
              searchValue = (e.target as HTMLInputElement).value;
            }}
            onfocusin={() => {
              comboboxOpen = true;
              searchValue = "";
            }}
            onclick={() => {
              comboboxOpen = true;
              searchValue = "";
            }}
            placeholder={m.account_profile_timezone_placeholder()}
            class="border-input bg-background dark:bg-input/30 placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-ring/50 flex h-9 w-full min-w-0 rounded-md border px-3 py-1 pr-8 text-base shadow-xs transition-[color,box-shadow] outline-none focus-visible:ring-[3px] md:text-sm"
          />
          <Combobox.Trigger class="absolute top-1/2 right-2 -translate-y-1/2">
            <ChevronsUpDownIcon class="size-4 opacity-50" />
          </Combobox.Trigger>
        </div>
        <Combobox.Content
          sideOffset={4}
          preventScroll={true}
          class="bg-popover text-popover-foreground data-[state=open]:animate-in data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:fade-in-0 data-[state=closed]:zoom-out-95 data-[state=open]:zoom-in-95 data-[side=bottom]:slide-in-from-top-2 relative z-50 max-h-60 min-w-[8rem] origin-(--bits-combobox-content-transform-origin) overflow-x-hidden overflow-y-auto rounded-md border shadow-md data-[side=bottom]:translate-y-1"
        >
          <Combobox.ScrollUpButton class="flex cursor-default items-center justify-center py-1">
            <ChevronUpIcon class="size-4" />
          </Combobox.ScrollUpButton>
          <Combobox.Viewport class="w-full min-w-(--bits-combobox-anchor-width) scroll-my-1 p-1">
            {#each filteredOptions as tz (tz.value)}
              <Combobox.Item
                value={tz.value}
                label={tz.label}
                class="data-[highlighted]:bg-accent data-[highlighted]:text-accent-foreground relative flex w-full cursor-pointer items-center gap-2 rounded-sm py-1.5 ps-2 pe-8 text-sm outline-hidden select-none"
              >
                {#snippet children({ selected })}
                  {tz.label}
                  <span class="absolute end-2 flex size-3.5 items-center justify-center">
                    {#if selected}
                      <CheckIcon class="size-4" />
                    {/if}
                  </span>
                {/snippet}
              </Combobox.Item>
            {:else}
              <div class="text-muted-foreground py-6 text-center text-sm">No results found.</div>
            {/each}
          </Combobox.Viewport>
          <Combobox.ScrollDownButton class="flex cursor-default items-center justify-center py-1">
            <ChevronDownIcon class="size-4" />
          </Combobox.ScrollDownButton>
        </Combobox.Content>
      </Combobox.Root>
    </div>

    <Dialog.Footer>
      <Button
        variant="ghost"
        onclick={() => (open = false)}
        disabled={saving}
      >
        {m.common_ui_cancel()}
      </Button>
      <Button
        onclick={apply}
        disabled={saving || !isDirty}
      >
        {#if saving}
          <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
        {/if}
        {m.common_ui_save()}
      </Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
