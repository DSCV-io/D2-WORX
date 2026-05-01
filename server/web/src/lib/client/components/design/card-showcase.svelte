<script lang="ts">
  import Section from "./section.svelte";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import { Avatar, AvatarFallback } from "$lib/client/components/ui/avatar/index.js";
  import { Progress } from "$lib/client/components/ui/progress/index.js";
  import BellIcon from "@lucide/svelte/icons/bell";
  import TrendingUpIcon from "@lucide/svelte/icons/trending-up";
</script>

<Section
  id="cards"
  title="Cards"
>
  <div class="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
    <!-- Profile card -->
    <Card.Root>
      <Card.Header>
        <div class="flex items-center gap-3">
          <Avatar>
            <AvatarFallback>JD</AvatarFallback>
          </Avatar>
          <div>
            <Card.Title>Jane Doe</Card.Title>
            <Card.Description>Product Manager</Card.Description>
          </div>
        </div>
      </Card.Header>
      <Card.Content>
        <p class="text-muted-foreground text-sm">
          Responsible for driving product strategy and managing the roadmap across 3 product lines.
        </p>
      </Card.Content>
      <Card.Footer class="flex gap-2">
        <Button size="sm">View Profile</Button>
        <Button
          size="sm"
          variant="outline">Message</Button
        >
      </Card.Footer>
    </Card.Root>

    <!-- Stats card -->
    <Card.Root>
      <Card.Header>
        <Card.Description>Monthly Revenue</Card.Description>
        <Card.Title class="text-3xl tabular-nums">$45,231</Card.Title>
      </Card.Header>
      <Card.Content>
        <div class="flex items-center gap-2 text-sm">
          <TrendingUpIcon class="size-4 text-green-600 dark:text-green-400" />
          <span class="font-medium text-green-600 dark:text-green-400">+20.1%</span>
          <span class="text-muted-foreground">from last month</span>
        </div>
      </Card.Content>
      <Card.Footer>
        <Progress
          value={72}
          class="w-full"
        />
      </Card.Footer>
    </Card.Root>

    <!-- Notification card -->
    <Card.Root>
      <Card.Header>
        <div class="flex items-center justify-between">
          <Card.Title class="text-base">Notifications</Card.Title>
          <Badge variant="secondary">3 new</Badge>
        </div>
      </Card.Header>
      <Card.Content>
        <div class="flex flex-col gap-3">
          {#each [{ title: "New comment on PR #42", time: "2 min ago" }, { title: "Deployment succeeded", time: "15 min ago" }, { title: "New team member joined", time: "1 hour ago" }] as item (item.title)}
            <div class="flex items-start gap-3">
              <BellIcon class="text-muted-foreground mt-0.5 size-4" />
              <div class="flex flex-col">
                <span class="text-sm font-medium">{item.title}</span>
                <span class="text-muted-foreground text-xs">{item.time}</span>
              </div>
            </div>
          {/each}
        </div>
      </Card.Content>
    </Card.Root>
  </div>
</Section>
