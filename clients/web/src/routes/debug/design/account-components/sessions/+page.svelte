<script lang="ts">
  import * as Card from "$lib/client/components/ui/card/index.js";
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Sessions</h2>
    <p class="text-muted-foreground text-sm">
      View and manage your active sessions across devices.
    </p>
  </div>

  <Card.Root>
    <Card.Content class="pt-6">
      <p class="text-muted-foreground text-sm">Session list will appear here.</p>
    </Card.Content>
  </Card.Root>
</div>
