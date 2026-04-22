<script
  lang="ts"
  generics="T extends Record<string, unknown>, U extends FormPath<T>"
>
  import type { Snippet } from "svelte";
  import type { HTMLInputAttributes } from "svelte/elements";
  import type { FormPath, SuperForm } from "sveltekit-superforms";
  import { Control } from "formsnap";
  import { cn } from "$lib/shared/utils/utils.js";
  import * as Form from "$lib/client/components/ui/form/index.js";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import FieldStatusIcon from "./field-status-icon.svelte";
  import { getFieldStatus, type FieldStatus } from "$lib/shared/forms/field-status.js";

  type Props = {
    form: SuperForm<T>;
    field: U;
    label: string;
    placeholder?: string;
    type?: string;
    description?: string;
    disabled?: boolean;
    /** Override the auto-derived status (e.g. for async validation). */
    status?: FieldStatus;
    /** Optional content rendered right-aligned on the label row. */
    labelRight?: Snippet;
    /** Optional clickable action inside the input (e.g. show/hide toggle). Renders at the trailing edge. */
    inputAction?: Snippet;
    /** Browser autocomplete hint. Defaults to "off". Set per-field for password manager integration. */
    autocomplete?: HTMLInputAttributes["autocomplete"];
    oninput?: (e: Event & { currentTarget: HTMLInputElement }) => void;
    onblur?: () => void;
  };

  let {
    form,
    field,
    label,
    placeholder = "",
    type = "text",
    description,
    disabled = false,
    status: statusOverride,
    labelRight,
    inputAction,
    autocomplete = "off",
    oninput,
    onblur,
  }: Props = $props();

  const formData = $derived(form.form);
  const errors = $derived(form.errors);
  const constraints = $derived(form.constraints);

  /** Show status icons only after the first blur+validate cycle. Resets on typing. */
  let showStatus = $state(false);

  let value = $derived(($formData as Record<string, string>)[field as string] ?? "");
  const fieldErrors = $derived(($errors as Record<string, string[]>)[field as string]);
  const isRequired = $derived(
    !!($constraints as Record<string, { required?: boolean }> | undefined)?.[field as string]
      ?.required,
  );
  const derivedStatus = $derived(
    showStatus ? getFieldStatus({ errors: fieldErrors, value }) : "idle",
  );
  const effectiveStatus = $derived(statusOverride ?? derivedStatus);

  function handleInput(e: Event & { currentTarget: HTMLInputElement }) {
    ($formData as Record<string, string>)[field as string] = e.currentTarget.value;
    // Clear status + errors while typing (re-validated on next blur)
    if (showStatus) {
      showStatus = false;
      errors.update((errs) => {
        const copy = { ...errs } as Record<string, unknown>;
        delete copy[field as string];
        return copy as typeof errs;
      });
    }
    oninput?.(e);
  }

  function handleBlur() {
    // Validate this field, then show status and fire callback
    (form.validate as (path: string) => Promise<unknown>)(field as string).then(() => {
      showStatus = true;
      onblur?.();
    });
  }
</script>

<Form.Field
  {form}
  name={field}
>
  <Control>
    {#snippet children({ props })}
      {#if labelRight}
        <div class="flex items-center justify-between">
          <Form.Label>
            {label}
            {#if isRequired}<span
                class="text-destructive"
                aria-hidden="true">*</span
              >{/if}
          </Form.Label>
          {@render labelRight()}
        </div>
      {:else}
        <Form.Label>
          {label}
          {#if isRequired}<span
              class="text-destructive"
              aria-hidden="true">*</span
            >{/if}
        </Form.Label>
      {/if}
      <div class="relative">
        <Input
          {...props}
          {type}
          {placeholder}
          {disabled}
          {value}
          {autocomplete}
          oninput={handleInput}
          onblur={handleBlur}
          class={cn(
            // Reserve right padding for trailing icons
            inputAction && effectiveStatus !== "idle" && "pr-14",
            inputAction && effectiveStatus === "idle" && "pr-8",
            !inputAction && effectiveStatus !== "idle" && "pr-8",
            effectiveStatus === "valid" && "border-2 border-success dark:border-success",
            effectiveStatus === "invalid" && "border-2",
          )}
        />
        {#if effectiveStatus !== "idle"}
          <div
            class={cn(
              "pointer-events-none absolute top-1/2 -translate-y-1/2",
              inputAction ? "right-8" : "right-2.5",
            )}
          >
            <FieldStatusIcon status={effectiveStatus} />
          </div>
        {/if}
        {#if inputAction}
          <div class="absolute top-0 right-2.5 flex h-full items-center">
            {@render inputAction()}
          </div>
        {/if}
      </div>
    {/snippet}
  </Control>
  {#if description}
    <Form.Description>{description}</Form.Description>
  {/if}
  <Form.FieldErrors />
</Form.Field>
