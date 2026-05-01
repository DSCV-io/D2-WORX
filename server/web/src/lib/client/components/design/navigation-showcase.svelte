<script lang="ts">
  import Section from "./section.svelte";
  import * as Tabs from "$lib/client/components/ui/tabs/index.js";
  import * as Accordion from "$lib/client/components/ui/accordion/index.js";
  import * as Card from "$lib/client/components/ui/card/index.js";
  import * as Breadcrumb from "$lib/client/components/ui/breadcrumb/index.js";
  import * as Pagination from "$lib/client/components/ui/pagination/index.js";
  import * as Command from "$lib/client/components/ui/command/index.js";
  import CalendarIcon from "@lucide/svelte/icons/calendar";
  import SmileIcon from "@lucide/svelte/icons/smile";
  import CalculatorIcon from "@lucide/svelte/icons/calculator";
  import SettingsIcon from "@lucide/svelte/icons/settings";
  import UserIcon from "@lucide/svelte/icons/user";
  import CreditCardIcon from "@lucide/svelte/icons/credit-card";
</script>

<Section
  id="navigation"
  title="Navigation"
>
  <div class="grid gap-6 md:grid-cols-2">
    <!-- Tabs -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Tabs</h3>
      <Card.Root>
        <Card.Content class="pt-6">
          <Tabs.Root value="overview">
            <Tabs.List>
              <Tabs.Trigger value="overview">Overview</Tabs.Trigger>
              <Tabs.Trigger value="analytics">Analytics</Tabs.Trigger>
              <Tabs.Trigger value="reports">Reports</Tabs.Trigger>
            </Tabs.List>
            <Tabs.Content
              value="overview"
              class="pt-4"
            >
              <p class="text-muted-foreground text-sm">
                Overview content goes here. This panel shows when the Overview tab is selected. Tabs
                are great for organizing related content into separate views.
              </p>
            </Tabs.Content>
            <Tabs.Content
              value="analytics"
              class="pt-4"
            >
              <p class="text-muted-foreground text-sm">
                Analytics dashboard content. Charts, graphs, and key metrics would be displayed in
                this panel.
              </p>
            </Tabs.Content>
            <Tabs.Content
              value="reports"
              class="pt-4"
            >
              <p class="text-muted-foreground text-sm">
                Reports section. Generated reports, export options, and historical data live here.
              </p>
            </Tabs.Content>
          </Tabs.Root>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Accordion -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Accordion</h3>
      <Card.Root>
        <Card.Content class="pt-6">
          <Accordion.Root type="single">
            <Accordion.Item value="item-1">
              <Accordion.Trigger>Is it accessible?</Accordion.Trigger>
              <Accordion.Content>
                Yes. Accordion follows the WAI-ARIA design pattern for disclosure widgets. It
                supports keyboard navigation and screen readers.
              </Accordion.Content>
            </Accordion.Item>
            <Accordion.Item value="item-2">
              <Accordion.Trigger>Is it styled?</Accordion.Trigger>
              <Accordion.Content>
                Yes. It comes with default styling that integrates with the shadcn-svelte theme
                system and your chosen color palette.
              </Accordion.Content>
            </Accordion.Item>
            <Accordion.Item value="item-3">
              <Accordion.Trigger>Is it animated?</Accordion.Trigger>
              <Accordion.Content>
                Yes. It uses CSS transitions for smooth open/close animations. The animation is
                subtle and respects reduced-motion preferences.
              </Accordion.Content>
            </Accordion.Item>
          </Accordion.Root>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Breadcrumb -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Breadcrumb</h3>
      <Card.Root>
        <Card.Content class="pt-6">
          <div class="flex flex-col gap-4">
            <Breadcrumb.Root>
              <Breadcrumb.List>
                <Breadcrumb.Item>
                  <Breadcrumb.Link href="#navigation">Home</Breadcrumb.Link>
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Link href="#navigation">Products</Breadcrumb.Link>
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Link href="#navigation">Category</Breadcrumb.Link>
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Page>Current Item</Breadcrumb.Page>
                </Breadcrumb.Item>
              </Breadcrumb.List>
            </Breadcrumb.Root>

            <Breadcrumb.Root>
              <Breadcrumb.List>
                <Breadcrumb.Item>
                  <Breadcrumb.Link href="#navigation">Home</Breadcrumb.Link>
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Ellipsis />
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Link href="#navigation">Category</Breadcrumb.Link>
                </Breadcrumb.Item>
                <Breadcrumb.Separator />
                <Breadcrumb.Item>
                  <Breadcrumb.Page>Current Item</Breadcrumb.Page>
                </Breadcrumb.Item>
              </Breadcrumb.List>
            </Breadcrumb.Root>
          </div>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Pagination -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Pagination</h3>
      <Card.Root>
        <Card.Content class="pt-6">
          <Pagination.Root
            count={100}
            perPage={10}
            siblingCount={1}
          >
            {#snippet children({ pages, currentPage })}
              <Pagination.Content>
                <Pagination.Item>
                  <Pagination.Previous />
                </Pagination.Item>
                {#each pages as page (page.key)}
                  {#if page.type === "ellipsis"}
                    <Pagination.Item>
                      <Pagination.Ellipsis />
                    </Pagination.Item>
                  {:else}
                    <Pagination.Item>
                      <Pagination.Link
                        {page}
                        isActive={currentPage === page.value}
                      >
                        {page.value}
                      </Pagination.Link>
                    </Pagination.Item>
                  {/if}
                {/each}
                <Pagination.Item>
                  <Pagination.Next />
                </Pagination.Item>
              </Pagination.Content>
            {/snippet}
          </Pagination.Root>
        </Card.Content>
      </Card.Root>
    </div>

    <!-- Command (search palette) -->
    <div class="flex flex-col gap-3 md:col-span-2">
      <h3 class="text-muted-foreground text-sm font-medium">Command Palette</h3>
      <Card.Root>
        <Card.Content class="pt-6">
          <Command.Root class="rounded-lg border shadow-md">
            <Command.Input placeholder="Type a command or search..." />
            <Command.List>
              <Command.Empty>No results found.</Command.Empty>
              <Command.Group heading="Suggestions">
                <Command.Item>
                  <CalendarIcon class="mr-2 size-4" />
                  Calendar
                </Command.Item>
                <Command.Item>
                  <SmileIcon class="mr-2 size-4" />
                  Search Emoji
                </Command.Item>
                <Command.Item>
                  <CalculatorIcon class="mr-2 size-4" />
                  Calculator
                </Command.Item>
              </Command.Group>
              <Command.Separator />
              <Command.Group heading="Settings">
                <Command.Item>
                  <UserIcon class="mr-2 size-4" />
                  Profile
                  <Command.Shortcut>Ctrl+P</Command.Shortcut>
                </Command.Item>
                <Command.Item>
                  <CreditCardIcon class="mr-2 size-4" />
                  Billing
                  <Command.Shortcut>Ctrl+B</Command.Shortcut>
                </Command.Item>
                <Command.Item>
                  <SettingsIcon class="mr-2 size-4" />
                  Settings
                  <Command.Shortcut>Ctrl+S</Command.Shortcut>
                </Command.Item>
              </Command.Group>
            </Command.List>
          </Command.Root>
        </Card.Content>
      </Card.Root>
    </div>
  </div>
</Section>
