<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import * as Select from "$lib/client/components/ui/select/index.js";
  import FieldStatusIcon from "./field-status-icon.svelte";
  import { getFieldStatus } from "$lib/shared/forms/field-status.js";

  type Option = { value: string; label: string };

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    options: Option[];
    placeholder?: string;
    description?: string;
    disabled?: boolean;
    onValueChange?: (value: string) => void;
  };

  let {
    form,
    field,
    label,
    options,
    placeholder = "Select...",
    description,
    disabled = false,
    onValueChange,
  }: Props = $props();

  const formData = $derived(form.form);
  const errors = $derived(form.errors);
  const constraints = $derived(form.constraints);
  let showStatus = $state(false);

  let value = $derived(($formData as Record<string, string>)[field as string] ?? "");
  const selectedLabel = $derived(options.find((o) => o.value === value)?.label ?? placeholder);
  const fieldErrors = $derived(($errors as Record<string, string[]>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );
  const fieldStatus = $derived(
    showStatus ? getFieldStatus({ errors: fieldErrors, value }) : "idle",
  );

  function handleChange(newValue: string | undefined) {
    if (newValue !== undefined) {
      ($formData as Record<string, string>)[field as string] = newValue;
      (form.validate as (path: string) => Promise<unknown>)(field as string).then(() => {
        showStatus = true;
      });
      onValueChange?.(newValue);
    }
  }
</script>

<Form.Field
  {form}
  name={field}
>
  <Control>
    {#snippet children({ props })}
      <Form.Label>
        {label}
        {#if isRequired}<span
            class="text-destructive"
            aria-hidden="true">*</span
          >{/if}
        <FieldStatusIcon status={fieldStatus} />
      </Form.Label>
      <Select.Root
        type="single"
        {value}
        onValueChange={handleChange}
        {disabled}
      >
        <Select.Trigger
          {...props}
          class="w-full"
        >
          <span class="truncate">{selectedLabel}</span>
        </Select.Trigger>
        <Select.Content>
          {#each options as option (option.value)}
            <Select.Item value={option.value}>{option.label}</Select.Item>
          {/each}
        </Select.Content>
      </Select.Root>
    {/snippet}
  </Control>
  {#if description}
    <Form.Description>{description}</Form.Description>
  {/if}
  <Form.FieldErrors />
</Form.Field>
