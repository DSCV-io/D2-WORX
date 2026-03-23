<script lang="ts">
  import * as Card from "$lib/client/components/ui/card/index.js";
</script>

<div class="space-y-6">
  <div>
    <h2 class="text-xl font-semibold">Recent Logins</h2>
    <p class="text-muted-foreground text-sm">
      Review your sign-in history and approximate locations.
    </p>
  </div>

  <Card.Root>
    <Card.Content class="pt-6">
      <p class="text-muted-foreground text-sm">Login history will appear here.</p>
    </Card.Content>
  </Card.Root>
</div>
