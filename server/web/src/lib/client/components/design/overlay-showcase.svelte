<script lang="ts">
  import Section from "./section.svelte";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import * as Sheet from "$lib/client/components/ui/sheet/index.js";
  import * as AlertDialog from "$lib/client/components/ui/alert-dialog/index.js";
  import * as Drawer from "$lib/client/components/ui/drawer/index.js";
  import * as ContextMenu from "$lib/client/components/ui/context-menu/index.js";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import { Label } from "$lib/client/components/ui/label/index.js";

  let goalValue = $state(350);
</script>

<Section
  id="overlays"
  title="Overlays"
>
  <div class="flex flex-col gap-6">
    <!-- Existing overlays row -->
    <div class="flex flex-wrap gap-4 rounded-lg border p-6">
      <!-- Dialog -->
      <Dialog.Root>
        <Dialog.Trigger>
          <Button variant="outline">Open Dialog</Button>
        </Dialog.Trigger>
        <Dialog.Content>
          <Dialog.Header>
            <Dialog.Title>Edit Profile</Dialog.Title>
            <Dialog.Description>
              Make changes to your profile here. Click save when you're done.
            </Dialog.Description>
          </Dialog.Header>
          <div class="grid gap-4 py-4">
            <div class="flex flex-col gap-2">
              <Label for="dialog-name">Name</Label>
              <Input
                id="dialog-name"
                value="Jane Doe"
              />
            </div>
            <div class="flex flex-col gap-2">
              <Label for="dialog-email">Email</Label>
              <Input
                id="dialog-email"
                value="jane@example.com"
              />
            </div>
          </div>
          <Dialog.Footer>
            <Button>Save changes</Button>
          </Dialog.Footer>
        </Dialog.Content>
      </Dialog.Root>

      <!-- Dropdown Menu -->
      <DropdownMenu.Root>
        <DropdownMenu.Trigger>
          <Button variant="outline">Open Menu</Button>
        </DropdownMenu.Trigger>
        <DropdownMenu.Content class="w-48">
          <DropdownMenu.Label>My Account</DropdownMenu.Label>
          <DropdownMenu.Separator />
          <DropdownMenu.Item>Profile</DropdownMenu.Item>
          <DropdownMenu.Item>Billing</DropdownMenu.Item>
          <DropdownMenu.Item>Settings</DropdownMenu.Item>
          <DropdownMenu.Separator />
          <DropdownMenu.Item>Log out</DropdownMenu.Item>
        </DropdownMenu.Content>
      </DropdownMenu.Root>

      <!-- Popover -->
      <Popover.Root>
        <Popover.Trigger>
          <Button variant="outline">Open Popover</Button>
        </Popover.Trigger>
        <Popover.Content class="w-72">
          <div class="flex flex-col gap-3">
            <h4 class="text-sm font-medium">Dimensions</h4>
            <p class="text-muted-foreground text-xs">Set the dimensions for the layer.</p>
            <div class="grid grid-cols-2 gap-2">
              <div class="flex flex-col gap-1">
                <Label
                  for="pop-w"
                  class="text-xs">Width</Label
                >
                <Input
                  id="pop-w"
                  value="100%"
                  class="h-8 text-xs"
                />
              </div>
              <div class="flex flex-col gap-1">
                <Label
                  for="pop-h"
                  class="text-xs">Height</Label
                >
                <Input
                  id="pop-h"
                  value="25px"
                  class="h-8 text-xs"
                />
              </div>
            </div>
          </div>
        </Popover.Content>
      </Popover.Root>

      <!-- Sheet -->
      <Sheet.Root>
        <Sheet.Trigger>
          <Button variant="outline">Open Sheet</Button>
        </Sheet.Trigger>
        <Sheet.Content side="left">
          <Sheet.Header>
            <Sheet.Title>Navigation</Sheet.Title>
            <Sheet.Description>Example side navigation panel.</Sheet.Description>
          </Sheet.Header>
          <div class="flex flex-col gap-2 py-4">
            <Button
              variant="ghost"
              class="justify-start">Dashboard</Button
            >
            <Button
              variant="ghost"
              class="justify-start">Settings</Button
            >
            <Button
              variant="ghost"
              class="justify-start">Help</Button
            >
          </div>
        </Sheet.Content>
      </Sheet.Root>

      <!-- Alert Dialog -->
      <AlertDialog.Root>
        <AlertDialog.Trigger>
          <Button variant="destructive">Delete Account</Button>
        </AlertDialog.Trigger>
        <AlertDialog.Content>
          <AlertDialog.Header>
            <AlertDialog.Title>Are you absolutely sure?</AlertDialog.Title>
            <AlertDialog.Description>
              This action cannot be undone. This will permanently delete your account and remove
              your data from our servers.
            </AlertDialog.Description>
          </AlertDialog.Header>
          <AlertDialog.Footer>
            <AlertDialog.Cancel>Cancel</AlertDialog.Cancel>
            <AlertDialog.Action
              class="bg-destructive hover:bg-destructive/90 dark:bg-destructive/60 dark:hover:bg-destructive/80 text-white"
              >Delete</AlertDialog.Action
            >
          </AlertDialog.Footer>
        </AlertDialog.Content>
      </AlertDialog.Root>

      <!-- Drawer -->
      <Drawer.Root>
        <Drawer.Trigger>
          <Button variant="outline">Open Drawer</Button>
        </Drawer.Trigger>
        <Drawer.Content>
          <Drawer.Header>
            <Drawer.Title>Move Goal</Drawer.Title>
            <Drawer.Description>Set your daily activity goal.</Drawer.Description>
          </Drawer.Header>
          <div class="flex flex-col items-center gap-4 p-4 pb-0">
            <div class="text-5xl font-bold tracking-tighter tabular-nums">
              {goalValue}
            </div>
            <p class="text-muted-foreground text-sm">calories/day</p>
            <div class="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                onclick={() => (goalValue = Math.max(0, goalValue - 10))}
              >
                -10
              </Button>
              <Button
                variant="outline"
                size="sm"
                onclick={() => (goalValue += 10)}
              >
                +10
              </Button>
            </div>
          </div>
          <Drawer.Footer>
            <Button>Submit</Button>
            <Drawer.Close>
              <Button
                variant="outline"
                class="w-full">Cancel</Button
              >
            </Drawer.Close>
          </Drawer.Footer>
        </Drawer.Content>
      </Drawer.Root>
    </div>

    <!-- Context Menu -->
    <div class="flex flex-col gap-3">
      <h3 class="text-muted-foreground text-sm font-medium">Context Menu</h3>
      <ContextMenu.Root>
        <ContextMenu.Trigger>
          <div
            class="text-muted-foreground flex h-36 items-center justify-center rounded-lg border border-dashed text-sm"
          >
            Right-click here
          </div>
        </ContextMenu.Trigger>
        <ContextMenu.Content class="w-56">
          <ContextMenu.Item>
            Back
            <ContextMenu.Shortcut>Alt+Left</ContextMenu.Shortcut>
          </ContextMenu.Item>
          <ContextMenu.Item>
            Forward
            <ContextMenu.Shortcut>Alt+Right</ContextMenu.Shortcut>
          </ContextMenu.Item>
          <ContextMenu.Item>
            Reload
            <ContextMenu.Shortcut>Ctrl+R</ContextMenu.Shortcut>
          </ContextMenu.Item>
          <ContextMenu.Separator />
          <ContextMenu.Sub>
            <ContextMenu.SubTrigger>More Tools</ContextMenu.SubTrigger>
            <ContextMenu.SubContent class="w-48">
              <ContextMenu.Item>Save Page As...</ContextMenu.Item>
              <ContextMenu.Item>Create Shortcut...</ContextMenu.Item>
              <ContextMenu.Item>Name Window...</ContextMenu.Item>
              <ContextMenu.Separator />
              <ContextMenu.Item>Developer Tools</ContextMenu.Item>
            </ContextMenu.SubContent>
          </ContextMenu.Sub>
          <ContextMenu.Separator />
          <ContextMenu.CheckboxItem checked={true}>
            Show Bookmarks Bar
            <ContextMenu.Shortcut>Ctrl+Shift+B</ContextMenu.Shortcut>
          </ContextMenu.CheckboxItem>
          <ContextMenu.CheckboxItem>Show Full URLs</ContextMenu.CheckboxItem>
        </ContextMenu.Content>
      </ContextMenu.Root>
    </div>
  </div>
</Section>
