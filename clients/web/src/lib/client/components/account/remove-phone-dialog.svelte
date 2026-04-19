<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { FormPasswordInput } from "$lib/client/components/forms/index.js";
  import { PASSWORD } from "$lib/shared/forms/field-presets.js";
  import { defaults, superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { z } from "zod";
  import { removePhone } from "$lib/client/rest/account-client.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { invalidateAll } from "$app/navigation";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  let { open = $bindable(false) }: { open?: boolean } = $props();

  const schema = z.object({
    currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
  });

  const form = superForm(defaults({ currentPassword: "" }, zodClient(schema)), {
    id: "remove-phone-form",
    validators: zodClient(schema),
    SPA: true,
    resetForm: false,
    async onUpdate({ form: f }) {
      if (!f.valid) return;
      submitting = true;
      errorMessage = "";
      try {
        const result = await removePhone(f.data.currentPassword);
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
        void invalidateAll();
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
      <Dialog.Title>{m.account_phone_remove_dialog_title()}</Dialog.Title>
      <Dialog.Description>{m.account_phone_remove_confirm_body()}</Dialog.Description>
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
          variant="destructive"
          disabled={submitting}
        >
          {#if submitting}
            <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
          {/if}
          {m.account_phone_remove_button()}
        </Button>
      </Dialog.Footer>
    </form>
  </Dialog.Content>
</Dialog.Root>
