<script lang="ts">
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { toast } from "svelte-sonner";
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Deactivate Account</h2>
    <p class="text-muted-foreground text-sm">Permanently remove your account and data.</p>
  </div>

  <Card.Root>
    <Card.Header>
      <Card.Title class="text-base">Delete Account</Card.Title>
      <Card.Description>
        Permanently delete your account and all associated data. This action cannot be undone.
      </Card.Description>
    </Card.Header>
    <Card.Content>
      <Button
        variant="destructive"
        onclick={() => toast.error("Account deactivation is not yet available.")}
      >
        Delete Account
      </Button>
    </Card.Content>
  </Card.Root>
</div>
