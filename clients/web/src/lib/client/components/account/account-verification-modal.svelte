<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import * as InputOTP from "$lib/client/components/ui/input-otp/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import {
    FormInput,
    FormPasswordInput,
    FormPhoneInput,
  } from "$lib/client/components/forms/index.js";
  import { EMAIL, PASSWORD } from "$lib/shared/forms/field-presets.js";
  import { defaults, superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { z } from "zod";
  import { untrack } from "svelte";
  import { emailField, phoneField } from "$lib/shared/forms/schemas.js";
  import { formatPhoneDisplay } from "$lib/shared/forms/phone-format.js";
  import { phoneToDigits } from "$lib/shared/utils/phone-format.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import type { CountryOption } from "$lib/shared/forms/geo-ref-data.js";
  import {
    requestEmailChange,
    verifyEmailChange,
    requestPhoneChange,
    verifyPhoneChange,
  } from "$lib/client/rest/account-client.js";
  import { invalidateAll } from "$app/navigation";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";
  import CircleCheckIcon from "@lucide/svelte/icons/circle-check";

  type Step = "input" | "confirm" | "otp" | "success";

  let {
    open = $bindable(false),
    type,
    countries = [],
    defaultCountry = "US",
  }: {
    open?: boolean;
    type: "email" | "phone";
    countries?: CountryOption[];
    defaultCountry?: string;
  } = $props();

  // Schema is selected per modal type. `type` is stable for the modal's
  // lifetime — read once at init via untrack to satisfy Svelte 5.
  const schema = untrack(() =>
    type === "email"
      ? z.object({
          newValue: emailField(),
          currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
        })
      : z.object({
          newValue: phoneField(),
          currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
        }),
  );

  const form = superForm(defaults({ newValue: "", currentPassword: "" }, zodClient(schema)), {
    id: untrack(() => `account-verify-${type}`),
    validators: zodClient(schema),
    SPA: true,
    resetForm: false,
    onUpdate({ form: f }) {
      if (!f.valid) return;
      // Validation passed — move to the confirmation step.
      // Actual submission happens from the confirm step's "Send Code" button.
      step = "confirm";
    },
  });

  const { enhance, form: formData, reset } = form;

  let step: Step = $state("input");
  let errorMessage = $state("");
  let submitting = $state(false);
  let expiresAt: Date | null = $state(null);
  let otpValue = $state("");
  let now = $state(Date.now());
  let resendCooldownUntil: number | null = $state(null);

  // Tick the clock every second while OTP step is active.
  $effect(() => {
    if (step !== "otp") return;
    const id = setInterval(() => {
      now = Date.now();
    }, 1000);
    return () => clearInterval(id);
  });

  // Reset state when modal closes.
  $effect(() => {
    if (!open) {
      setTimeout(() => {
        step = "input";
        errorMessage = "";
        otpValue = "";
        expiresAt = null;
        resendCooldownUntil = null;
        reset();
      }, 200);
    }
  });

  // Auto-submit verify when 6 digits are entered.
  $effect(() => {
    if (step === "otp" && otpValue.length === 6 && !submitting) {
      void doVerify();
    }
  });

  // ---- Derived display values ----

  const displayValue = $derived(() => {
    const v = $formData.newValue ?? "";
    if (type === "email") return v.trim();
    return formatPhoneDisplay(v, defaultCountry);
  });

  const remainingSeconds = $derived(() => {
    if (!expiresAt) return 0;
    return Math.max(0, Math.ceil((expiresAt.getTime() - now) / 1000));
  });

  const expiryLabel = $derived(() => {
    const s = remainingSeconds();
    const mm = Math.floor(s / 60);
    const ss = s % 60;
    return `${mm}:${ss.toString().padStart(2, "0")}`;
  });

  const resendCooldownSec = $derived(() =>
    resendCooldownUntil ? Math.max(0, Math.ceil((resendCooldownUntil - now) / 1000)) : 0,
  );

  // ---- Actions ----

  async function doRequest() {
    submitting = true;
    errorMessage = "";
    const newValue = $formData.newValue;
    const currentPassword = $formData.currentPassword;
    try {
      const result =
        type === "email"
          ? await requestEmailChange(newValue.trim(), currentPassword)
          : await requestPhoneChange(phoneToDigits(newValue), currentPassword);

      if (!result.success) {
        if (result.statusCode === 401) {
          errorMessage = m.account_password_incorrect();
          $formData.currentPassword = "";
          step = "input";
          return;
        }
        if (result.statusCode === 409) {
          errorMessage =
            type === "email" ? m.account_otp_email_in_use() : m.account_otp_phone_in_use();
          step = "input";
          return;
        }
        if (result.statusCode === 429) {
          errorMessage = m.account_otp_rate_limited();
          return;
        }
        if (result.errorCode === "PHONE_NO_CHANGE") {
          errorMessage = m.account_otp_no_change_phone();
          step = "input";
          return;
        }
        errorMessage = translateMessage(result.messages?.[0], undefined, m.common_errors_unknown());
        return;
      }

      const exp = result.data?.expiresAt;
      expiresAt = exp ? new Date(exp) : null;
      otpValue = "";
      $formData.currentPassword = ""; // clear from memory
      resendCooldownUntil = Date.now() + 30_000;
      step = "otp";
    } finally {
      submitting = false;
    }
  }

  async function doVerify() {
    if (otpValue.length !== 6) return;
    submitting = true;
    errorMessage = "";
    try {
      const result =
        type === "email" ? await verifyEmailChange(otpValue) : await verifyPhoneChange(otpValue);

      if (!result.success) {
        // Always clear otpValue on failure — the auto-submit $effect re-fires
        // whenever otpValue.length === 6, so leaving the value in place causes
        // an infinite request loop on errors that don't naturally clear it
        // (e.g. UNHANDLED_EXCEPTION).
        otpValue = "";
        if (result.statusCode === 401) {
          errorMessage = m.account_otp_invalid_code();
          return;
        }
        if (result.statusCode === 404) {
          errorMessage = m.account_otp_expired();
          return;
        }
        if (result.statusCode === 429) {
          errorMessage = m.account_otp_max_attempts();
          return;
        }
        errorMessage = translateMessage(result.messages?.[0], undefined, m.common_errors_unknown());
        return;
      }

      step = "success";
      void invalidateAll();
      setTimeout(() => {
        open = false;
      }, 1500);
    } finally {
      submitting = false;
    }
  }

  function resend() {
    if (resendCooldownSec() > 0) return;
    // Resending requires the password again — force user back to input.
    step = "input";
    otpValue = "";
    errorMessage = "";
    expiresAt = null;
  }

  function back() {
    errorMessage = "";
    step = "input";
  }

  function dialogTitle(): string {
    if (step === "input" || step === "confirm") {
      return type === "email"
        ? m.account_email_change_dialog_title()
        : m.account_phone_change_dialog_title();
    }
    if (step === "otp") {
      return m.account_email_change_otp_title();
    }
    return type === "email" ? m.account_email_change_success() : m.account_phone_change_success();
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content
    class="max-w-sm"
    onInteractOutside={(e) => e.preventDefault()}
    onEscapeKeydown={(e) => e.preventDefault()}
  >
    <Dialog.Header>
      <Dialog.Title>{dialogTitle()}</Dialog.Title>
      {#if step === "otp"}
        <Dialog.Description>
          {#if type === "email"}
            {m.account_email_change_otp_subtitle({ newEmail: displayValue() })}
          {:else}
            {m.account_phone_otp_subtitle({ newPhone: displayValue() })}
          {/if}
        </Dialog.Description>
      {/if}
    </Dialog.Header>

    {#if step === "input"}
      <form
        method="POST"
        use:enhance
        class="flex flex-col gap-4 py-2"
      >
        {#if type === "email"}
          <FormInput
            {form}
            field="newValue"
            {...EMAIL}
          />
        {:else}
          <FormPhoneInput
            {form}
            field="newValue"
            label={m.account_phone_input_label()}
            {countries}
            {defaultCountry}
          />
        {/if}
        <FormPasswordInput
          {form}
          field="currentPassword"
          {...PASSWORD}
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
            disabled={submitting}>{m.account_email_change_continue()}</Button
          >
        </Dialog.Footer>
      </form>
    {:else if step === "confirm"}
      <div class="flex flex-col gap-4 py-2">
        <p class="text-sm">
          {#if type === "email"}
            {m.account_email_change_confirm_body({ newEmail: displayValue() })}
          {:else}
            {m.account_phone_change_confirm_body({ newPhone: displayValue() })}
          {/if}
        </p>
        {#if errorMessage}
          <p class="text-destructive text-sm">{errorMessage}</p>
        {/if}
      </div>

      <Dialog.Footer>
        <Button
          variant="ghost"
          onclick={back}
          disabled={submitting}>{m.common_ui_back()}</Button
        >
        <Button
          onclick={doRequest}
          disabled={submitting}
        >
          {#if submitting}
            <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
          {/if}
          {m.account_email_change_send_code()}
        </Button>
      </Dialog.Footer>
    {:else if step === "otp"}
      <div class="flex flex-col items-center gap-4 py-4">
        <InputOTP.Root
          maxlength={6}
          bind:value={otpValue}
          disabled={submitting}
        >
          {#snippet children({ cells })}
            <InputOTP.Group>
              {#each cells as cell, i (i)}
                <InputOTP.Slot {cell} />
              {/each}
            </InputOTP.Group>
          {/snippet}
        </InputOTP.Root>

        <p class="text-muted-foreground text-sm">
          {#if remainingSeconds() > 0}
            {m.account_otp_expires_in({ time: expiryLabel() })}
          {:else}
            <span class="text-destructive">{m.account_otp_expired()}</span>
          {/if}
        </p>

        {#if errorMessage}
          <p class="text-destructive text-sm">{errorMessage}</p>
        {/if}
      </div>

      <Dialog.Footer>
        <Button
          variant="ghost"
          onclick={resend}
          disabled={submitting || resendCooldownSec() > 0}
        >
          {#if resendCooldownSec() > 0}
            {m.account_otp_resend_in({ seconds: resendCooldownSec().toString() })}
          {:else}
            {m.account_otp_resend()}
          {/if}
        </Button>
        <Button
          variant="ghost"
          onclick={() => (open = false)}>{m.common_ui_cancel()}</Button
        >
      </Dialog.Footer>
    {:else if step === "success"}
      <div class="flex flex-col items-center gap-3 py-6">
        <CircleCheckIcon class="size-12 text-green-500" />
        <p class="text-sm">
          {type === "email" ? m.account_email_change_success() : m.account_phone_change_success()}
        </p>
      </div>
    {/if}
  </Dialog.Content>
</Dialog.Root>
