<script lang="ts">
  import type { SuperValidated } from "sveltekit-superforms";
  import { superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { resolve } from "$app/paths";
  import {
    createForgotPasswordSchema,
    type ForgotPasswordFormData,
  } from "$lib/shared/forms/forgot-password-schema.js";
  import { FormInput } from "$lib/client/components/forms/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import TextLink from "$lib/client/components/ui/text-link.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { EMAIL } from "$lib/shared/forms/field-presets.js";
  import * as m from "$lib/paraglide/messages.js";
  import MailIcon from "@lucide/svelte/icons/mail";

  type Props = {
    data: SuperValidated<ForgotPasswordFormData>;
  };

  let { data }: Props = $props();

  const schema = createForgotPasswordSchema();

  let submitting = $state(false);
  let serverError = $state("");
  let sent = $state(false);
  let sentEmail = $state("");

  function initForm() {
    return superForm(data, {
      id: "forgot-password-form",
      validators: zodClient(schema),
      SPA: true,
      async onUpdate({ form: f }) {
        if (!f.valid) return;

        submitting = true;
        serverError = "";

        try {
          const { email } = f.data;
          const result = await authClient.requestPasswordReset({
            email,
            redirectTo: "/reset-password",
          });

          if (result.error) {
            if (result.error.status === 429) {
              serverError = m.common_errors_TOO_MANY_REQUESTS();
              return;
            }
            serverError = result.error.message ?? m.common_errors_unknown();
            return;
          }

          sentEmail = email;
          sent = true;
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
</script>

{#if sent}
  <Card.Header class="text-center">
    <div class="bg-muted mx-auto mb-2 flex size-12 items-center justify-center rounded-full">
      <MailIcon class="text-muted-foreground size-6" />
    </div>
    <Card.Title class="text-2xl">{m.auth_forgot_password_sent_title()}</Card.Title>
  </Card.Header>
  <Card.Content class="flex flex-col gap-3 text-center">
    <p class="text-sm">{m.auth_forgot_password_sent_description()}</p>
    {#if sentEmail}
      <p class="font-medium">{sentEmail}</p>
    {/if}
    <p class="text-muted-foreground text-sm">{m.auth_forgot_password_sent_note()}</p>
  </Card.Content>
  <Card.Footer>
    <Button
      variant="outline"
      href={resolve("/sign-in")}
      class="w-full"
    >
      {m.common_ui_sign_in()}
    </Button>
  </Card.Footer>
{:else}
  <Card.Header>
    <Card.Title class="text-2xl">{m.auth_forgot_password_title()}</Card.Title>
    <Card.Description>{m.auth_forgot_password_description()}</Card.Description>
  </Card.Header>
  <Card.Content>
    <form
      method="POST"
      use:enhance
      autocomplete="off"
      class="flex flex-col gap-5"
    >
      <FormInput
        {form}
        field="email"
        {...EMAIL}
      />

      {#if serverError}
        <p class="text-destructive text-sm">{serverError}</p>
      {/if}

      <Button
        type="submit"
        disabled={submitting}
        class="w-full"
      >
        {submitting ? m.auth_forgot_password_submitting() : m.auth_forgot_password_submit()}
      </Button>

      <p class="text-muted-foreground text-center text-sm">
        <TextLink href={resolve("/sign-in")}>
          {m.auth_forgot_password_back()}
        </TextLink>
      </p>
    </form>
  </Card.Content>
{/if}
