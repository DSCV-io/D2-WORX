<script lang="ts">
  import Section from "./section.svelte";
  import { Avatar, AvatarFallback, AvatarImage } from "$lib/client/components/ui/avatar/index.js";
  import { Badge } from "$lib/client/components/ui/badge/index.js";
  import * as Alert from "$lib/client/components/ui/alert/index.js";
  import * as Tooltip from "$lib/client/components/ui/tooltip/index.js";
  import { Toggle } from "$lib/client/components/ui/toggle/index.js";
  import { Separator } from "$lib/client/components/ui/separator/index.js";
  import { ScrollArea } from "$lib/client/components/ui/scroll-area/index.js";
  import * as Table from "$lib/client/components/ui/table/index.js";
  import * as Collapsible from "$lib/client/components/ui/collapsible/index.js";
  import * as Carousel from "$lib/client/components/ui/carousel/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import { AspectRatio } from "$lib/client/components/ui/aspect-ratio/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import CircleAlertIcon from "@lucide/svelte/icons/circle-alert";
  import InfoIcon from "@lucide/svelte/icons/info";
  import AlertTriangleIcon from "@lucide/svelte/icons/triangle-alert";
  import OctagonXIcon from "@lucide/svelte/icons/octagon-x";
  import CheckCircleIcon from "@lucide/svelte/icons/circle-check";
  import BoldIcon from "@lucide/svelte/icons/bold";
  import ItalicIcon from "@lucide/svelte/icons/italic";
  import UnderlineIcon from "@lucide/svelte/icons/underline";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import ImageIcon from "@lucide/svelte/icons/image";

  const tableData = [
    { id: "INV001", status: "Paid", method: "Credit Card", amount: "$250.00" },
    { id: "INV002", status: "Pending", method: "PayPal", amount: "$150.00" },
    { id: "INV003", status: "Unpaid", method: "Bank Transfer", amount: "$350.00" },
    { id: "INV004", status: "Paid", method: "Credit Card", amount: "$450.00" },
    { id: "INV005", status: "Paid", method: "PayPal", amount: "$550.00" },
  ];

  let collapsibleOpen = $state(false);

  const carouselItems = [
    { title: "Slide 1", color: "bg-primary/10" },
    { title: "Slide 2", color: "bg-secondary/30" },
    { title: "Slide 3", color: "bg-accent/30" },
    { title: "Slide 4", color: "bg-muted" },
    { title: "Slide 5", color: "bg-primary/20" },
  ];
</script>

<Section
  id="data-display"
  title="Data Display"
>
  <div class="flex flex-col gap-6">
    <!-- Avatars -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Avatars</h3>
      <div class="flex items-center gap-4 rounded-lg border p-6">
        <Avatar class="size-8">
          <AvatarFallback>SM</AvatarFallback>
        </Avatar>
        <Avatar>
          <AvatarFallback>MD</AvatarFallback>
        </Avatar>
        <Avatar class="size-12">
          <AvatarFallback>LG</AvatarFallback>
        </Avatar>
        <Avatar class="size-16">
          <AvatarFallback>XL</AvatarFallback>
        </Avatar>
        <Avatar>
          <AvatarImage
            src="https://api.dicebear.com/9.x/initials/svg?seed=JD"
            alt="JD"
          />
          <AvatarFallback>JD</AvatarFallback>
        </Avatar>
      </div>
    </div>

    <!-- Badges -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Badges</h3>
      <div class="flex flex-wrap items-center gap-3 rounded-lg border p-6">
        <Badge>Default</Badge>
        <Badge variant="secondary">Secondary</Badge>
        <Badge variant="accent">New</Badge>
        <Badge variant="destructive">Destructive</Badge>
        <Badge variant="outline">Outline</Badge>
      </div>
    </div>

    <!-- Status Badges -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Status Badges</h3>
      <div class="flex flex-wrap items-center gap-3 rounded-lg border p-6">
        <Badge class="bg-info text-info-foreground hover:bg-info/80 border-transparent">Info</Badge>
        <Badge class="bg-success text-success-foreground hover:bg-success/80 border-transparent"
          >Success</Badge
        >
        <Badge class="bg-warning text-warning-foreground hover:bg-warning/80 border-transparent"
          >Warning</Badge
        >
        <Badge
          class="bg-destructive text-destructive-foreground hover:bg-destructive/80 border-transparent"
          >Danger</Badge
        >
      </div>
    </div>

    <!-- Alerts -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Alerts</h3>
      <div class="flex flex-col gap-3">
        <Alert.Root>
          <CircleAlertIcon />
          <Alert.Title>Heads up!</Alert.Title>
          <Alert.Description>
            You can add components to your app using the CLI or by copying and pasting.
          </Alert.Description>
        </Alert.Root>

        <Alert.Root variant="info">
          <InfoIcon />
          <Alert.Title>Info</Alert.Title>
          <Alert.Description>
            A new software update is available. See what's new in version 4.0.
          </Alert.Description>
        </Alert.Root>

        <Alert.Root variant="success">
          <CheckCircleIcon />
          <Alert.Title>Success</Alert.Title>
          <Alert.Description>Your changes have been saved successfully.</Alert.Description>
        </Alert.Root>

        <Alert.Root variant="warning">
          <AlertTriangleIcon />
          <Alert.Title>Warning</Alert.Title>
          <Alert.Description>
            Your trial expires in 3 days. Upgrade now to keep your data.
          </Alert.Description>
        </Alert.Root>

        <Alert.Root variant="destructive">
          <OctagonXIcon />
          <Alert.Title>Error</Alert.Title>
          <Alert.Description>
            Your session has expired. Please log in again to continue.
          </Alert.Description>
        </Alert.Root>
      </div>
    </div>

    <!-- Tooltips + Toggle -->
    <div class="grid gap-6 md:grid-cols-2">
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Tooltips</h3>
        <div class="flex gap-3 rounded-lg border p-6">
          <Tooltip.Provider>
            <Tooltip.Root>
              <Tooltip.Trigger>
                <Button
                  variant="outline"
                  size="sm">Hover me</Button
                >
              </Tooltip.Trigger>
              <Tooltip.Content>
                <p>This is a tooltip message</p>
              </Tooltip.Content>
            </Tooltip.Root>
          </Tooltip.Provider>
        </div>
      </div>

      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Toggle</h3>
        <div class="flex gap-2 rounded-lg border p-6">
          <Toggle aria-label="Toggle bold">
            <BoldIcon class="size-4" />
          </Toggle>
          <Toggle aria-label="Toggle italic">
            <ItalicIcon class="size-4" />
          </Toggle>
          <Toggle aria-label="Toggle underline">
            <UnderlineIcon class="size-4" />
          </Toggle>
        </div>
      </div>
    </div>

    <!-- Collapsible -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Collapsible</h3>
      <div class="rounded-lg border p-6">
        <Collapsible.Root
          bind:open={collapsibleOpen}
          class="w-full max-w-sm"
        >
          <div class="flex items-center justify-between">
            <h4 class="text-sm font-semibold">@peduarte starred 3 repositories</h4>
            <Collapsible.Trigger>
              <Button
                variant="ghost"
                size="sm"
              >
                <ChevronsUpDownIcon class="size-4" />
                <span class="sr-only">Toggle</span>
              </Button>
            </Collapsible.Trigger>
          </div>
          <div class="mt-2 rounded-md border px-4 py-2 text-sm">@radix-ui/primitives</div>
          <Collapsible.Content>
            <div class="mt-2 flex flex-col gap-2">
              <div class="rounded-md border px-4 py-2 text-sm">@radix-ui/colors</div>
              <div class="rounded-md border px-4 py-2 text-sm">@stitches/react</div>
            </div>
          </Collapsible.Content>
        </Collapsible.Root>
      </div>
    </div>

    <!-- Carousel -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Carousel</h3>
      <div class="mx-auto w-full max-w-md rounded-lg border p-6">
        <Carousel.Root class="w-full">
          <Carousel.Content>
            {#each carouselItems as item, i (i)}
              <Carousel.Item>
                <div class="p-1">
                  <Card.Root>
                    <Card.Content
                      class="flex aspect-square items-center justify-center p-6 {item.color}"
                    >
                      <span class="text-3xl font-semibold">{i + 1}</span>
                    </Card.Content>
                  </Card.Root>
                </div>
              </Carousel.Item>
            {/each}
          </Carousel.Content>
          <Carousel.Previous />
          <Carousel.Next />
        </Carousel.Root>
      </div>
    </div>

    <!-- Aspect Ratio -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Aspect Ratio</h3>
      <div class="grid gap-4 md:grid-cols-2">
        <div class="flex flex-col gap-2">
          <span class="text-muted-foreground text-xs">16:9</span>
          <AspectRatio
            ratio={16 / 9}
            class="bg-muted rounded-lg border"
          >
            <div class="text-muted-foreground flex h-full items-center justify-center gap-2">
              <ImageIcon class="size-6" />
              <span class="text-sm">16:9 Content</span>
            </div>
          </AspectRatio>
        </div>
        <div class="flex flex-col gap-2">
          <span class="text-muted-foreground text-xs">1:1</span>
          <AspectRatio
            ratio={1}
            class="bg-muted rounded-lg border"
          >
            <div class="text-muted-foreground flex h-full items-center justify-center gap-2">
              <ImageIcon class="size-6" />
              <span class="text-sm">1:1 Content</span>
            </div>
          </AspectRatio>
        </div>
      </div>
    </div>

    <!-- Separator -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Separator</h3>
      <div class="rounded-lg border p-6">
        <div class="flex flex-col gap-1">
          <h4 class="text-sm font-medium">DCSV WORX</h4>
          <p class="text-muted-foreground text-xs">Business tools for contractors and operators</p>
        </div>
        <Separator class="my-4" />
        <div class="flex h-5 items-center gap-4 text-sm">
          <span>Docs</span>
          <Separator orientation="vertical" />
          <span>Source</span>
          <Separator orientation="vertical" />
          <span>Support</span>
        </div>
      </div>
    </div>

    <!-- Scroll Area -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Scroll Area</h3>
      <ScrollArea class="h-48 rounded-lg border">
        <div class="p-4">
          {#each Array.from({ length: 20 }, (_, i) => i + 1) as n (n)}
            <div class="py-2 text-sm">
              Item {n} — This is a scrollable list item demonstrating the scroll area component.
            </div>
            {#if n < 20}
              <Separator />
            {/if}
          {/each}
        </div>
      </ScrollArea>
    </div>

    <!-- Table -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Table</h3>
      <div class="rounded-lg border">
        <Table.Root>
          <Table.Header>
            <Table.Row>
              <Table.Head class="w-24">Invoice</Table.Head>
              <Table.Head>Status</Table.Head>
              <Table.Head>Method</Table.Head>
              <Table.Head class="text-right">Amount</Table.Head>
            </Table.Row>
          </Table.Header>
          <Table.Body>
            {#each tableData as row (row.id)}
              <Table.Row>
                <Table.Cell class="font-medium">{row.id}</Table.Cell>
                <Table.Cell>
                  <Badge
                    variant={row.status === "Paid"
                      ? "default"
                      : row.status === "Pending"
                        ? "secondary"
                        : "outline"}
                  >
                    {row.status}
                  </Badge>
                </Table.Cell>
                <Table.Cell>{row.method}</Table.Cell>
                <Table.Cell class="text-right">{row.amount}</Table.Cell>
              </Table.Row>
            {/each}
          </Table.Body>
        </Table.Root>
      </div>
    </div>
  </div>
</Section>
