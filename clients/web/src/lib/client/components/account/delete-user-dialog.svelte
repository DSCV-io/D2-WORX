<script lang="ts">
  import * as m from "$lib/paraglide/messages.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { FormInput, FormPasswordInput, FormTextarea } from "$lib/client/components/forms/index.js";
  import { defaults, superForm } from "sveltekit-superforms";
  import { zod4Client as zodClient } from "sveltekit-superforms/adapters";
  import { z } from "zod";
  import { requestUserDeletion } from "$lib/client/rest/account-client.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
  import { authClient } from "$lib/client/stores/auth-client.js";
  import { invalidateToken } from "$lib/client/rest/gateway-client.js";
  import { goto } from "$app/navigation";
  import { resolve } from "$app/paths";
  import LoaderCircleIcon from "@lucide/svelte/icons/loader-circle";
  import { page } from "$app/stores";

  let { open = $bindable(false) }: { open?: boolean } = $props();

  // Username comes from session — typed-confirm matches against this.
  const expectedUsername = $derived(
    (
      ($page.data.user as { username?: string; displayUsername?: string } | null)?.displayUsername ??
        ($page.data.user as { username?: string } | null)?.username ??
        ""
    )
      .toLowerCase()
      .trim(),
  );

  // Schema validates all three fields. Username typed-confirm uses `superRefine`
  // because the expected value is dynamic (session-derived) — can't be a string
  // literal in the schema.
  // `reason` / `comment` use empty-string defaults (not `.optional()`) so the
  // schema infers a `Record<string, unknown>`-compatible shape that satisfies
  // FormInput's generic constraint. We submit them as undefined when blank.
  const schema = z
    .object({
      typedUsername: z.string().min(1, { error: () => m.webclient_forms_required() }),
      currentPassword: z.string().min(1, { error: () => m.webclient_forms_required() }),
      reason: z.string().max(200).default(""),
      comment: z.string().max(2000).default(""),
    })
    .superRefine((data, ctx) => {
      if (data.typedUsername.toLowerCase().trim() !== expectedUsername) {
        ctx.addIssue({
          code: "custom",
          path: ["typedUsername"],
          message: m.account_delete_typed_confirm_mismatch(),
        });
      }
    });

  const form = superForm(
    defaults(
      { typedUsername: "", currentPassword: "", reason: "", comment: "" },
      zodClient(schema),
    ),
    {
      id: "delete-user-form",
      validators: zodClient(schema),
      SPA: true,
      resetForm: false,
      async onUpdate({ form: f }) {
        if (!f.valid) return;
        submitting = true;
        errorMessage = "";
        try {
          const feedback =
            f.data.reason || f.data.comment
              ? {
                  reason: f.data.reason || undefined,
                  comment: f.data.comment || undefined,
                }
              : undefined;

          const result = await requestUserDeletion(f.data.currentPassword, feedback);

          if (!result.success) {
            if (result.statusCode === 401) {
              errorMessage = m.account_password_incorrect();
              $formData.currentPassword = "";
              return;
            }
            if (result.statusCode === 409) {
              // Sole-owner block — clear and explicit copy with link to orgs.
              errorMessage = m.account_delete_blocked_sole_owner();
              return;
            }
            errorMessage = translateMessage(
              result.messages?.[0],
              undefined,
              m.common_errors_unknown(),
            );
            return;
          }

          // Server already revoked the session row + busted the BetterAuth
          // Redis cookie cache, but the BROWSER still has the session cookie
          // and the in-memory JWT until we explicitly clear them. Mirrors the
          // standard sign-out flow (see public-nav.svelte::handleSignOut):
          //   1. authClient.signOut() — wipes the SvelteKit session cookie
          //   2. invalidateToken() — clears the gateway-client JWT cache
          //   3. goto({ invalidateAll: true }) — re-runs data loaders so the
          //      UI reflects the now-unauthenticated state on the public page
          // signOut() may 401 since the server-side row is already gone — we
          // intentionally swallow that; the cookie wipe still happens via the
          // BetterAuth Set-Cookie response header.
          await authClient.signOut().catch(() => {});
          invalidateToken();
          open = false;
          await goto(resolve("/auth/account-deletion-scheduled"), { invalidateAll: true });
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
    class="max-w-md"
    onInteractOutside={(e) => e.preventDefault()}
    onEscapeKeydown={(e) => e.preventDefault()}
  >
    <Dialog.Header>
      <Dialog.Title class="text-destructive">{m.account_delete_dialog_title()}</Dialog.Title>
      <Dialog.Description>{m.account_delete_dialog_body()}</Dialog.Description>
    </Dialog.Header>

    <p class="text-destructive bg-destructive/10 rounded-md p-3 text-sm">
      {m.account_delete_dialog_warning()}
    </p>

    <form
      method="POST"
      use:enhance
      class="flex flex-col gap-4 py-2"
    >
      <FormInput
        {form}
        field="typedUsername"
        label={m.account_delete_typed_confirm_label()}
        placeholder={m.account_delete_typed_confirm_placeholder()}
        autocomplete="off"
        disabled={submitting}
      />
      <FormPasswordInput
        {form}
        field="currentPassword"
        label={m.account_delete_password_label()}
        placeholder={m.account_delete_password_placeholder()}
        autocomplete="current-password"
        disabled={submitting}
      />

      <details class="text-muted-foreground text-sm">
        <summary class="cursor-pointer select-none">{m.account_delete_feedback_label()}</summary>
        <div class="mt-3 flex flex-col gap-3">
          <FormInput
            {form}
            field="reason"
            label={m.account_delete_feedback_reason_placeholder()}
            placeholder={m.account_delete_feedback_reason_placeholder()}
            disabled={submitting}
          />
          <FormTextarea
            {form}
            field="comment"
            label={m.account_delete_feedback_comment_placeholder()}
            placeholder={m.account_delete_feedback_comment_placeholder()}
            rows={3}
            disabled={submitting}
          />
        </div>
      </details>

      {#if errorMessage}
        <p class="text-destructive text-sm">{errorMessage}</p>
      {/if}

      <Dialog.Footer>
        <Button
          type="button"
          variant="ghost"
          onclick={() => (open = false)}
          disabled={submitting}
        >
          {m.account_delete_cancel_button()}
        </Button>
        <Button
          type="submit"
          variant="destructive"
          disabled={submitting}
        >
          {#if submitting}
            <LoaderCircleIcon class="mr-2 size-4 animate-spin" />
            {m.account_delete_confirm_submitting()}
          {:else}
            {m.account_delete_confirm_button()}
          {/if}
        </Button>
      </Dialog.Footer>
    </form>
  </Dialog.Content>
</Dialog.Root>
