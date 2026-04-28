<script lang="ts">
  import { cn } from "$lib/shared/utils/utils.js";
  import { Switch } from "$lib/client/components/ui/switch/index.js";
  import * as m from "$lib/paraglide/messages.js";

  let {
    value = $bindable(false),
    label,
    description,
    onSave,
    class: className,
  }: {
    value?: boolean;
    label: string;
    description?: string;
    onSave: (value: boolean) => Promise<void>;
    class?: string;
  } = $props();

  let saving = $state(false);
  let errorMessage = $state("");

  // Immediate-save semantics: every toggle commits to the server. Matches the
  // industry convention for switches in settings UIs (iOS, GitHub, Discord).
  // No save/revert affordance — the toggle IS the action. On failure the
  // displayed value snaps back so the UI never lies about persisted state.
  async function handleChange(checked: boolean) {
    const previous = value;
    value = checked;
    saving = true;
    errorMessage = "";
    try {
      await onSave(checked);
    } catch (err) {
      value = previous;
      errorMessage = err instanceof Error ? err.message : m.common_errors_save_failed();
    } finally {
      saving = false;
    }
  }

  const fieldId = $derived(label.toLowerCase().replace(/\s+/g, "-"));
</script>

<div
  class={cn("flex flex-col gap-1.5", className)}
  data-slot="inline-switch"
>
  <div class="flex items-center justify-between gap-3">
    <div class="flex flex-col gap-0.5">
      <label
        class="text-sm font-medium"
        for={fieldId}>{label}</label
      >
      {#if description}
        <p class="text-muted-foreground text-xs">{description}</p>
      {/if}
    </div>
    <Switch
      id={fieldId}
      checked={value}
      onCheckedChange={handleChange}
      disabled={saving}
    />
  </div>

  {#if errorMessage}
    <p class="text-destructive text-xs">{errorMessage}</p>
  {/if}
</div>
