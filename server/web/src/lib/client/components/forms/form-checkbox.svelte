<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import { Checkbox } from "$lib/client/components/ui/checkbox/index.js";

  // Note: Checkbox uses required * only (no status icon / green border)
  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    description?: string;
    disabled?: boolean;
  };

  let { form, field, label, description, disabled = false }: Props = $props();

  const formData = $derived(form.form);
  const constraints = $derived(form.constraints);
  let checked = $derived(!!($formData as Record<string, boolean>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );

  function handleChange(value: boolean | "indeterminate") {
    ($formData as Record<string, boolean>)[field as string] = value === true;
  }
</script>

<Form.Field
  {form}
  name={field}
>
  <Control>
    {#snippet children({ props })}
      <div class="flex items-start gap-3">
        <Checkbox
          {...props}
          {checked}
          onCheckedChange={handleChange}
          {disabled}
        />
        <div class="space-y-1 leading-none">
          <Form.Label class="text-sm font-normal">
            {label}
            {#if isRequired}<span
                class="text-destructive"
                aria-hidden="true">*</span
              >{/if}
          </Form.Label>
          {#if description}
            <Form.Description>{description}</Form.Description>
          {/if}
        </div>
      </div>
    {/snippet}
  </Control>
  <Form.FieldErrors />
</Form.Field>
