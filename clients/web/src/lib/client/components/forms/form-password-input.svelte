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
  };

  let { form, field, label, placeholder, description, disabled = false }: Props = $props();

  let showPassword = $state(false);
</script>

<FormInput
  {form}
  {field}
  {label}
  {placeholder}
  {description}
  {disabled}
  type={showPassword ? "text" : "password"}
>
  {#snippet inputAction()}
    <button
      type="button"
      onclick={() => (showPassword = !showPassword)}
      class="text-muted-foreground hover:text-foreground"
      aria-label={showPassword ? m.webclient_forms_hide_password() : m.webclient_forms_show_password()}
    >
      {#if showPassword}
        <EyeOffIcon class="size-4" />
      {:else}
        <EyeIcon class="size-4" />
      {/if}
    </button>
  {/snippet}
</FormInput>
