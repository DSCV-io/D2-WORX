<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import FormInput from "./form-input.svelte";
  import EyeIcon from "@lucide/svelte/icons/eye";
  import EyeOffIcon from "@lucide/svelte/icons/eye-off";
  import * as m from "$lib/paraglide/messages.js";

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    placeholder?: string;
    description?: string;
    disabled?: boolean;
    /** Password-manager hint — `current-password` for sign-in/verify, `new-password` for change/reset. */
    autocomplete?: "current-password" | "new-password" | "off";
    /**
     * Override for the visibility-toggle button's `aria-label`. Required when
     * a screen contains more than one password field (sign-up, reset, change),
     * otherwise both buttons share the same accessible name and screen-reader
     * users / role-based locators can't disambiguate them. Defaults to the
     * generic "Show / Hide password" pair.
     */
    toggleLabel?: { show: string; hide: string };
  };

  let {
    form,
    field,
    label,
    placeholder,
    description,
    disabled = false,
    autocomplete,
    toggleLabel,
  }: Props = $props();

  const showLabel = $derived(toggleLabel?.show ?? m.webclient_forms_show_password());
  const hideLabel = $derived(toggleLabel?.hide ?? m.webclient_forms_hide_password());

  let showPassword = $state(false);
</script>

<FormInput
  {form}
  {field}
  {label}
  {placeholder}
  {description}
  {disabled}
  {autocomplete}
  type={showPassword ? "text" : "password"}
>
  {#snippet inputAction()}
    <button
      type="button"
      onclick={() => (showPassword = !showPassword)}
      class="text-muted-foreground hover:text-foreground"
      aria-label={showPassword ? hideLabel : showLabel}
    >
      {#if showPassword}
        <EyeOffIcon class="size-4" />
      {:else}
        <EyeIcon class="size-4" />
      {/if}
    </button>
  {/snippet}
</FormInput>
