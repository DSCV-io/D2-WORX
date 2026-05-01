<script lang="ts">
  import LanguagesIcon from "@lucide/svelte/icons/languages";
  import CheckIcon from "@lucide/svelte/icons/check";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import { getLocale, setLocale, type Locale } from "$lib/paraglide/runtime";
  import * as m from "$lib/paraglide/messages.js";
  import { page } from "$app/stores";
  import type { LocaleOption } from "$lib/shared/forms/locale-options.js";

  const locales: LocaleOption[] = $derived(
    ($page.data as { localeOptions?: LocaleOption[] }).localeOptions ?? [],
  );
</script>

<DropdownMenu.Root>
  <DropdownMenu.Trigger>
    {#snippet child({ props })}
      <Button
        {...props}
        variant="ghost"
        size="icon"
        aria-label={m.webclient_language_label()}
      >
        <LanguagesIcon class="size-4" />
      </Button>
    {/snippet}
  </DropdownMenu.Trigger>
  <DropdownMenu.Content align="end">
    {#each locales as locale (locale.code)}
      <DropdownMenu.Item onclick={() => setLocale(locale.code as Locale)}>
        {#if locale.flag}
          <img
            src={locale.flag}
            alt=""
            class="h-3 w-4 shrink-0 object-cover"
          />
        {/if}
        {locale.endonym}
        {#if getLocale() === locale.code}
          <CheckIcon class="ml-auto size-4" />
        {/if}
      </DropdownMenu.Item>
    {/each}
  </DropdownMenu.Content>
</DropdownMenu.Root>
