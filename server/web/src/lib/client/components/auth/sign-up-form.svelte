<script lang="ts">
  import type { SuperValidated } from "sveltekit-superforms";
  import { superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { createSignUpSchema, type SignUpFormData } from "$lib/shared/forms/sign-up-schema.js";
  import { FormInput, FormPasswordInput } from "$lib/client/components/forms/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import TextLink from "$lib/client/components/ui/text-link.svelte";
  import { useAsyncFieldCheck } from "$lib/client/forms/async-field-check.svelte.js";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { authApiCall } from "$lib/client/rest/auth-gateway-client.js";
  import {
    FIRST_NAME,
    LAST_NAME,
    EMAIL,
    CONFIRM_EMAIL,
    PASSWORD,
    CONFIRM_PASSWORD,
  } from "$lib/shared/forms/field-presets.js";
  import { maskDisplayName } from "$lib/client/utils/mask-display-name.js";
  import * as m from "$lib/paraglide/messages.js";

  type Props = {
    data: SuperValidated<SignUpFormData>;
  };

  let { data }: Props = $props();

  const schema = createSignUpSchema();

  let submitting = $state(false);
  let serverError = $state("");

  function initForm() {
    return superForm(data, {
      id: "sign-up-form",
      validators: zodClient(schema),
      SPA: true,
      async onUpdate({ form: f }) {
        if (!f.valid) return;

        submitting = true;
        serverError = "";

        try {
          const { firstName, lastName, email, password } = f.data;
          const result = await authClient.signUp.email({
            name: `${firstName} ${lastName}`,
            email,
            password,
          });

          if (result.error) {
            // Always show server errors at the form level — field-level errors
            // get cleared by client-side revalidation on the next interaction.
            // BetterAuth's `result.error.message` is raw English; ignore it and
            // render a single localized fallback.
            serverError = m.auth_sign_in_sign_up_failed();
            f.valid = false;
            return;
          }

          const verifyUrl = resolve("/verify-email") + `?email=${encodeURIComponent(email)}`;
          // eslint-disable-next-line svelte/no-navigation-without-resolve -- pre-resolved above
          await goto(verifyUrl);
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

  const emailCheck = useAsyncFieldCheck({
    form,
    field: "email",
    preCheck: (v) => !!v && v.includes("@"),
    async checker(email) {
      const result = await authApiCall<{ available: boolean }>(
        `/api/auth/check-email?email=${encodeURIComponent(email)}`,
      );
      if (!result.success) return { valid: true }; // Fail-open on server error
      return {
        valid: result.data?.available !== false,
        errorMessage: m.auth_errors_EMAIL_ALREADY_TAKEN(),
      };
    },
  });
</script>

<form
  method="POST"
  use:enhance
  autocomplete="off"
  class="flex flex-col gap-5"
>
  <div class="grid gap-4 sm:grid-cols-2">
    <FormInput
      {form}
      field="firstName"
      {...FIRST_NAME}
      oninput={maskDisplayName}
    />
    <FormInput
      {form}
      field="lastName"
      {...LAST_NAME}
      oninput={maskDisplayName}
    />
  </div>

  <FormInput
    {form}
    field="email"
    {...EMAIL}
    status={emailCheck.status === "idle" ? undefined : emailCheck.status}
    onblur={emailCheck.check}
    oninput={emailCheck.reset}
  />

  <FormInput
    {form}
    field="confirmEmail"
    {...CONFIRM_EMAIL}
  />

  <FormPasswordInput
    {form}
    field="password"
    {...PASSWORD}
  />

  <FormPasswordInput
    {form}
    field="confirmPassword"
    {...CONFIRM_PASSWORD}
    toggleLabel={{
      show: m.webclient_forms_show_confirm_password(),
      hide: m.webclient_forms_hide_confirm_password(),
    }}
  />

  {#if serverError}
    <p class="text-destructive text-sm">{serverError}</p>
  {/if}

  <Button
    type="submit"
    disabled={submitting}
    class="w-full"
  >
    {submitting ? m.auth_sign_up_submitting() : m.auth_sign_up_submit()}
  </Button>

  <p class="text-muted-foreground text-center text-sm">
    {m.auth_sign_up_has_account()}
    <TextLink href={resolve("/sign-in")}>{m.auth_sign_up_link()}</TextLink>
  </p>
</form>
