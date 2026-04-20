<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { FormPasswordInput } from "$lib/client/components/forms/index.js";
  import { PASSWORD } from "$lib/shared/forms/field-presets.js";
  import { defaults, superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { z } from "zod";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import type { D2Result } from "@d2/result";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  /**
   * Generic password-confirmation modal for security-tab destructive actions.
   *
   * The caller supplies an `onSubmit(password)` async fn that performs the
   * actual mutation and returns a D2Result. This component owns the modal,
   * input state, validation, and 401 handling — so each call site stays thin.
   */
  let {
    open = $bindable(false),
    title,
    description,
    confirmLabel,
    confirmVariant = "default",
    onSubmit,
    onSuccess,
  }: {
    open?: boolean;
    title: string;
    description: string;
    confirmLabel: string;
    confirmVariant?: "default" | "destructive";
    onSubmit: (password: string) => Promise<D2Result>;
    onSuccess?: () => void | Promise<void>;
  } = $props();

  const schema = z.object({
    currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
  });

  const form = superForm(defaults({ currentPassword: "" }, zodClient(schema)), {
    id: "password-confirm-form",
    validators: zodClient(schema),
    SPA: true,
    resetForm: false,
    async onUpdate({ form: f }) {
      if (!f.valid) return;
      submitting = true;
      errorMessage = "";
      try {
        const result = await onSubmit(f.data.currentPassword);
        if (!result.success) {
          if (result.statusCode === 401) {
            errorMessage = m.account_password_incorrect();
            $formData.currentPassword = "";
            return;
          }
          errorMessage = translateMessage(
            result.messages?.[0],
            undefined,
            m.common_errors_unknown(),
          );
          return;
        }
        await onSuccess?.();
        open = false;
      } finally {
        submitting = false;
      }
    },
  });

  const { enhance, form: formData, reset } = form;

  let errorMessage = $state("");
  let submitting = $state(false);

  $effect(() => {
    if (!open) {
      setTimeout(() => {
        errorMessage = "";
        reset();
      }, 200);
    }
  });
</script>

<Dialog.Root bind:open>
  <Dialog.Content
    class="max-w-sm"
    onInteractOutside={(e) => e.preventDefault()}
    onEscapeKeydown={(e) => e.preventDefault()}
  >
    <Dialog.Header>
      <Dialog.Title>{title}</Dialog.Title>
      <Dialog.Description>{description}</Dialog.Description>
    </Dialog.Header>

    <form
      method="POST"
      use:enhance
      class="flex flex-col gap-4 py-2"
    >
      <FormPasswordInput
        {form}
        field="currentPassword"
        {...PASSWORD}
        disabled={submitting}
      />
      {#if errorMessage}
        <p class="text-destructive text-sm">{errorMessage}</p>
      {/if}

      <Dialog.Footer>
        <Button
          type="button"
          variant="ghost"
          onclick={() => (open = false)}
          disabled={submitting}>{m.common_ui_cancel()}</Button
        >
        <Button
          type="submit"
          variant={confirmVariant}
          disabled={submitting}
        >
          {#if submitting}
            <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
          {/if}
          {confirmLabel}
        </Button>
      </Dialog.Footer>
    </form>
  </Dialog.Content>
</Dialog.Root>
