<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { FormPasswordInput, FormCheckbox } from "$lib/client/components/forms/index.js";
  import { defaults, superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { z } from "zod";
  import { passwordField } from "$lib/shared/forms/schemas.js";
  import { changePassword } from "$lib/client/rest/account-client.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { toast } from "svelte-sonner";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";

  let { open = $bindable(false) }: { open?: boolean } = $props();

  // Mirrors the auth-domain password policy enforced server-side by
  // `createPasswordFunctions().hash` (length, numeric-only, date-like).
  // The HIBP breach check + common-password blocklist stay server-side
  // (network call + large dictionary), so wrong-input still surfaces from
  // the API as a 400 with a translated message.
  const schema = z
    .object({
      currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
      newPassword: passwordField(),
      confirmPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
      revokeOtherSessions: z.boolean(),
    })
    .refine((d) => d.newPassword === d.confirmPassword, {
      path: ["confirmPassword"],
      error: () => m.webclient_app_account_change_password_mismatch(),
    })
    .refine((d) => d.newPassword !== d.currentPassword, {
      path: ["newPassword"],
      error: () => m.webclient_app_account_change_password_same_as_current(),
    });

  const form = superForm(
    defaults(
      { currentPassword: "", newPassword: "", confirmPassword: "", revokeOtherSessions: true },
      zodClient(schema),
    ),
    {
      id: "change-password-form",
      validators: zodClient(schema),
      SPA: true,
      resetForm: false,
      async onUpdate({ form: f }) {
        if (!f.valid) return;
        submitting = true;
        errorMessage = "";
        try {
          const result = await changePassword(
            f.data.currentPassword,
            f.data.newPassword,
            f.data.revokeOtherSessions,
          );
          if (!result.success) {
            if (result.statusCode === 401) {
              errorMessage = m.webclient_app_account_password_incorrect();
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
          toast.success(m.webclient_app_account_security_password_changed());
          // Backend's account.update.after hook pushes user:updated via SignalR
          // → root layout listener busts session cache + invalidateAll(). No
          // local invalidate needed — single source of truth for cache-bust.
          open = false;
        } finally {
          submitting = false;
        }
      },
    },
  );

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
      <Dialog.Title>{m.webclient_app_account_security_change_password_title()}</Dialog.Title>
      <Dialog.Description
        >{m.webclient_app_account_security_change_password_description()}</Dialog.Description
      >
    </Dialog.Header>

    <form
      method="POST"
      use:enhance
      class="flex flex-col gap-4 py-2"
    >
      <FormPasswordInput
        {form}
        field="currentPassword"
        label={m.webclient_app_account_change_password_current_label()}
        placeholder={m.webclient_app_account_change_password_current_placeholder()}
        autocomplete="current-password"
        disabled={submitting}
        toggleLabel={{
          show: m.webclient_forms_show_current_password(),
          hide: m.webclient_forms_hide_current_password(),
        }}
      />
      <FormPasswordInput
        {form}
        field="newPassword"
        label={m.webclient_app_account_change_password_new_label()}
        placeholder={m.webclient_app_account_change_password_new_placeholder()}
        autocomplete="new-password"
        disabled={submitting}
        toggleLabel={{
          show: m.webclient_forms_show_new_password(),
          hide: m.webclient_forms_hide_new_password(),
        }}
      />
      <FormPasswordInput
        {form}
        field="confirmPassword"
        label={m.webclient_app_account_change_password_confirm_label()}
        placeholder={m.webclient_app_account_change_password_confirm_placeholder()}
        autocomplete="new-password"
        disabled={submitting}
        toggleLabel={{
          show: m.webclient_forms_show_confirm_password(),
          hide: m.webclient_forms_hide_confirm_password(),
        }}
      />
      <FormCheckbox
        {form}
        field="revokeOtherSessions"
        label={m.webclient_app_account_change_password_revoke_others_label()}
        description={m.webclient_app_account_change_password_revoke_others_description()}
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
          disabled={submitting}
        >
          {#if submitting}
            <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
          {/if}
          {m.webclient_app_account_security_change_password_yes()}
        </Button>
      </Dialog.Footer>
    </form>
  </Dialog.Content>
</Dialog.Root>
