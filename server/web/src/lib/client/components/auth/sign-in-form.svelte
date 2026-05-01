<script lang="ts">
  import type { SuperValidated } from "sveltekit-superforms";
  import { superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { goto, invalidateAll } from "$app/navigation";
  import { resolve } from "$app/paths";
  import { createSignInSchema, type SignInFormData } from "$lib/shared/forms/sign-in-schema.js";
  import { FormInput, FormPasswordInput } from "$lib/client/components/forms/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import TextLink from "$lib/client/components/ui/text-link.svelte";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { EMAIL, PASSWORD } from "$lib/shared/forms/field-presets.js";
  import * as m from "$lib/paraglide/messages.js";

  type Props = {
    data: SuperValidated<SignInFormData>;
    returnTo?: string | null;
  };

  let { data, returnTo }: Props = $props();

  const schema = createSignInSchema();

  let submitting = $state(false);
  let serverError = $state("");

  function initForm() {
    return superForm(data, {
      id: "sign-in-form",
      validators: zodClient(schema),
      SPA: true,
      async onUpdate({ form: f }) {
        if (!f.valid) return;

        submitting = true;
        serverError = "";

        try {
          const { email, password } = f.data;
          const result = await authClient.signIn.email({ email, password });

          if (result.error) {
            const status = result.error.status;

            // 403 = email not verified → redirect to verify-email page
            if (status === 403) {
              const verifyUrl =
                resolve("/verify-email") + `?email=${encodeURIComponent(email)}&resent=true`;
              // eslint-disable-next-line svelte/no-navigation-without-resolve -- pre-resolved above
              await goto(verifyUrl);
              return;
            }

            // 429 = throttled
            if (status === 429) {
              serverError = m.auth_errors_SIGN_IN_THROTTLED();
              return;
            }

            // Always render a localized error — BetterAuth's `result.error.message`
            // is raw English so we ignore it and rely on a single translated fallback.
            serverError = m.auth_sign_in_invalid_credentials();
            return;
          }

          // Invalidate all loaders so $page.data.session is fresh before navigation.
          await invalidateAll();

          // returnTo comes from the URL (already locale-prefixed); only the fallback needs resolve().
          const dest =
            returnTo && returnTo.startsWith("/") && !returnTo.startsWith("//")
              ? returnTo
              : resolve("/dashboard");
          // eslint-disable-next-line svelte/no-navigation-without-resolve -- dest is pre-resolved above
          await goto(dest);
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

  <FormPasswordInput
    {form}
    field="password"
    {...PASSWORD}
  />

  {#if serverError}
    <p class="text-destructive text-sm">{serverError}</p>
  {/if}

  <Button
    type="submit"
    disabled={submitting}
    class="w-full"
  >
    {submitting ? m.auth_sign_in_submitting() : m.auth_sign_in_submit()}
  </Button>

  <p class="text-muted-foreground text-center text-sm">
    {m.auth_sign_in_no_account()}
    <TextLink href={resolve("/sign-up")}>{m.auth_sign_in_link()}</TextLink>
  </p>

  <p class="text-muted-foreground text-center text-sm">
    <TextLink href={resolve("/forgot-password")}>{m.auth_sign_in_forgot_password()}</TextLink>
  </p>
</form>
