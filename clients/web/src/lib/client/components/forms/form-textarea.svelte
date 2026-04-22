<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import { Textarea } from "$lib/client/components/ui/textarea/index.js";
  import FieldStatusIcon from "./field-status-icon.svelte";
  import { getFieldStatus } from "$lib/shared/forms/field-status.js";

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    placeholder?: string;
    description?: string;
    disabled?: boolean;
    rows?: number;
  };

  let {
    form,
    field,
    label,
    placeholder = "",
    description,
    disabled = false,
    rows = 3,
  }: Props = $props();

  const formData = $derived(form.form);
  const errors = $derived(form.errors);
  const constraints = $derived(form.constraints);
  let showStatus = $state(false);

  let value = $derived(($formData as Record<string, string>)[field as string] ?? "");
  const fieldErrors = $derived(($errors as Record<string, string[]>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );
  const fieldStatus = $derived(
    showStatus ? getFieldStatus({ errors: fieldErrors, value }) : "idle",
  );

  function handleInput() {
    if (showStatus) {
      showStatus = false;
      errors.update((errs) => {
        const copy = { ...errs } as Record<string, unknown>;
        delete copy[field as string];
        return copy as typeof errs;
      });
    }
  }

  function handleBlur() {
    (form.validate as (path: string) => Promise<unknown>)(field as string).then(() => {
      showStatus = true;
    });
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
      <Textarea
        {...props}
        {placeholder}
        {disabled}
        {rows}
        oninput={handleInput}
        onblur={handleBlur}
        class={cn(
          fieldStatus === "valid" && "border-2 border-success dark:border-success",
          fieldStatus === "invalid" && "border-2",
        )}
      />
    {/snippet}
  </Control>
  {#if description}
    <Form.Description>{description}</Form.Description>
  {/if}
  <Form.FieldErrors />
</Form.Field>
