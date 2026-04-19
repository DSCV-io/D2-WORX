<script lang="ts">
  import type { SuperValidated } from "sveltekit-superforms";
  import { superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import {
    createResetPasswordSchema,
    type ResetPasswordFormData,
  } from "$lib/shared/forms/reset-password-schema.js";
  import { FormPasswordInput } from "$lib/client/components/forms/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { NEW_PASSWORD, CONFIRM_NEW_PASSWORD } from "$lib/shared/forms/field-presets.js";
  import * as m from "$lib/paraglide/messages.js";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";
  import CircleXIcon from "@lucide/svelte/icons/circle-x";

  type Props = {
    data: SuperValidated<ResetPasswordFormData>;
    token: string | null;
  };

  let { data, token }: Props = $props();

  const schema = createResetPasswordSchema();

  let submitting = $state(false);
  let serverError = $state("");
  let result = $state<"success" | "token-error" | null>(null);
  let countdown = $state(5);

  function initForm() {
    return superForm(data, {
      id: "reset-password-form",
      validators: zodClient(schema),
      SPA: true,
      async onUpdate({ form: f }) {
        if (!f.valid || !token) return;

        submitting = true;
        serverError = "";

        try {
          const res = await authClient.resetPassword({ newPassword: f.data.newPassword, token });

          if (res.error) {
            // Token errors — can't retry with same token
            if (res.error.status === 401 || res.error.status === 403) {
              result = "token-error";
              return;
            }

            if (res.error.status === 429) {
              serverError = m.common_errors_TOO_MANY_REQUESTS();
              return;
            }

            // Password validation or other errors — show inline (user can retry)
            serverError = res.error.message ?? m.common_errors_unknown();
            return;
          }

          result = "success";
        } catch {
          serverError = m.common_errors_unknown();
        } finally {
          submitting = false;
        }
      },
    });
  }

  const form = initForm();

  const { enhance } = form;

  $effect(() => {
    if (result !== "success") return;

    const interval = setInterval(() => {
      countdown--;
      if (countdown <= 0) {
        clearInterval(interval);
        goto(resolve("/sign-in"));
      }
    }, 1000);

    return () => clearInterval(interval);
  });
</script>

{#if !token || result === "token-error"}
  <!-- No token or invalid/expired token -->
  <Card.Header class="text-center">
    <div
      class="bg-destructive/10 mx-auto mb-2 flex size-12 items-center justify-center rounded-full"
    >
      <CircleXIcon class="text-destructive size-6" />
    </div>
    <Card.Title class="text-2xl">{m.auth_reset_password_invalid_token_title()}</Card.Title>
  </Card.Header>
  <Card.Content class="flex flex-col gap-3 text-center">
    <p class="text-sm">{m.auth_reset_password_invalid_token_description()}</p>
  </Card.Content>
  <Card.Footer>
    <Button
      href={resolve("/forgot-password")}
      class="w-full"
    >
      {m.auth_reset_password_request_new()}
    </Button>
  </Card.Footer>
{:else if result === "success"}
  <!-- Password reset successful -->
  <Card.Header class="text-center">
    <div class="bg-success/10 mx-auto mb-2 flex size-12 items-center justify-center rounded-full">
      <CircleCheckIcon class="text-success size-6" />
    </div>
    <Card.Title class="text-2xl">{m.auth_reset_password_success_title()}</Card.Title>
  </Card.Header>
  <Card.Content class="flex flex-col gap-3 text-center">
    <p class="text-sm">{m.auth_reset_password_success_description()}</p>
    <p class="text-muted-foreground text-sm">
      {m.auth_reset_password_success_redirect({ seconds: String(countdown) })}
    </p>
  </Card.Content>
  <Card.Footer>
    <Button
      href={resolve("/sign-in")}
      class="w-full">{m.common_ui_sign_in()}</Button
    >
  </Card.Footer>
{:else}
  <!-- Reset password form -->
  <Card.Header>
    <Card.Title class="text-2xl">{m.auth_reset_password_title()}</Card.Title>
    <Card.Description>{m.auth_reset_password_description()}</Card.Description>
  </Card.Header>
  <Card.Content>
    <form
      method="POST"
      use:enhance
      autocomplete="off"
      class="flex flex-col gap-5"
    >
      <FormPasswordInput
        {form}
        field="newPassword"
        {...NEW_PASSWORD}
      />

      <FormPasswordInput
        {form}
        field="confirmNewPassword"
        {...CONFIRM_NEW_PASSWORD}
      />

      {#if serverError}
        <p class="text-destructive text-sm">{serverError}</p>
      {/if}

      <Button
        type="submit"
        disabled={submitting}
        class="w-full"
      >
        {submitting ? m.auth_reset_password_submitting() : m.auth_reset_password_submit()}
      </Button>
    </form>
  </Card.Content>
{/if}
