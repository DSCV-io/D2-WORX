<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold text-red-500">Danger Zone</h2>
    <p class="text-muted-foreground text-sm">Irreversible account actions.</p>
  </div>

  <Card.Root class="border-destructive/50">
    <Card.Header>
      <Card.Title class="text-destructive text-base">Delete Account</Card.Title>
      <Card.Description>
        Permanently delete your account and all associated data. This cannot be undone.
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Button
        variant="destructive"
        onclick={() => toast.error("Account deletion is not yet available.")}
      >
        Delete Account
      </Button>
    </Card.Content>
  </Card.Root>
</div>
