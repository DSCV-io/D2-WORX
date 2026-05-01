<script lang="ts">
  import Section from "./section.svelte";
  import { Input } from "$lib/client/components/ui/input/index.js";
  import { Textarea } from "$lib/client/components/ui/textarea/index.js";
  import { Label } from "$lib/client/components/ui/label/index.js";
  import { Checkbox } from "$lib/client/components/ui/checkbox/index.js";
  import { RadioGroup, RadioGroupItem } from "$lib/client/components/ui/radio-group/index.js";
  import { Switch } from "$lib/client/components/ui/switch/index.js";
  import * as Select from "$lib/client/components/ui/select/index.js";
  import { Slider } from "$lib/client/components/ui/slider/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import { Calendar } from "$lib/client/components/ui/calendar/index.js";
  import * as ToggleGroup from "$lib/client/components/ui/toggle-group/index.js";
  import * as Popover from "$lib/client/components/ui/popover/index.js";
  import * as Command from "$lib/client/components/ui/command/index.js";
  import { today, getLocalTimeZone } from "@internationalized/date";
  import type { DateValue } from "@internationalized/date";
  import CheckIcon from "@lucide/svelte/icons/check";
  import ChevronsUpDownIcon from "@lucide/svelte/icons/chevrons-up-down";
  import AlignLeftIcon from "@lucide/svelte/icons/align-left";
  import AlignCenterIcon from "@lucide/svelte/icons/align-center";
  import AlignRightIcon from "@lucide/svelte/icons/align-right";
  import AlignJustifyIcon from "@lucide/svelte/icons/align-justify";
  import WifiIcon from "@lucide/svelte/icons/wifi";
  import BluetoothIcon from "@lucide/svelte/icons/bluetooth";
  import MonitorIcon from "@lucide/svelte/icons/monitor";

  let switchChecked = $state(false);
  let checkboxChecked = $state(false);
  let sliderValue = $state(50);
  let selectValue = $state<string | undefined>(undefined);
  let calendarValue = $state<DateValue | undefined>(today(getLocalTimeZone()));
  let alignValue = $state("left");
  let connectivityValues = $state<string[]>(["wifi"]);
  let comboOpen = $state(false);
  let comboValue = $state("");

  const frameworks = [
    { value: "sveltekit", label: "SvelteKit" },
    { value: "nextjs", label: "Next.js" },
    { value: "nuxt", label: "Nuxt" },
    { value: "remix", label: "Remix" },
    { value: "astro", label: "Astro" },
  ];

  const selectedFrameworkLabel = $derived(
    frameworks.find((f) => f.value === comboValue)?.label ?? "Select framework...",
  );
</script>

<Section
  id="forms"
  title="Form Controls"
>
  <div class="grid gap-6 md:grid-cols-2">
    <!-- Text inputs -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Text Inputs</h3>

      <div class="flex flex-col gap-2">
        <Label for="email">Email</Label>
        <Input
          id="email"
          type="email"
          placeholder="you@example.com"
        />
      </div>

      <div class="flex flex-col gap-2">
        <Label for="password">Password</Label>
        <Input
          id="password"
          type="password"
          placeholder="Enter password"
        />
      </div>

      <div class="flex flex-col gap-2">
        <Label for="disabled-input">Disabled</Label>
        <Input
          id="disabled-input"
          placeholder="Can't touch this"
          disabled
        />
      </div>

      <div class="flex flex-col gap-2">
        <Label for="message">Message</Label>
        <Textarea
          id="message"
          placeholder="Type your message here..."
        />
      </div>
    </div>

    <!-- Selection controls -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Selection Controls</h3>

      <div class="flex flex-col gap-3">
        <Label>Select</Label>
        <Select.Root
          type="single"
          bind:value={selectValue}
        >
          <Select.Trigger class="w-full">
            <span class="truncate">{selectValue || "Select a fruit..."}</span>
          </Select.Trigger>
          <Select.Content>
            <Select.Item value="apple">Apple</Select.Item>
            <Select.Item value="banana">Banana</Select.Item>
            <Select.Item value="cherry">Cherry</Select.Item>
            <Select.Item value="grape">Grape</Select.Item>
          </Select.Content>
        </Select.Root>
      </div>

      <div class="flex items-center gap-2">
        <Checkbox
          id="terms"
          bind:checked={checkboxChecked}
        />
        <Label
          for="terms"
          class="text-sm">Accept terms and conditions</Label
        >
      </div>

      <div class="flex items-center gap-2">
        <Switch
          id="notifications"
          bind:checked={switchChecked}
        />
        <Label
          for="notifications"
          class="text-sm"
        >
          Enable notifications {switchChecked ? "(on)" : "(off)"}
        </Label>
      </div>

      <div class="flex flex-col gap-3">
        <Label>Preferred contact</Label>
        <RadioGroup value="email">
          <div class="flex items-center gap-2">
            <RadioGroupItem
              value="email"
              id="r-email"
            />
            <Label
              for="r-email"
              class="text-sm font-normal">Email</Label
            >
          </div>
          <div class="flex items-center gap-2">
            <RadioGroupItem
              value="phone"
              id="r-phone"
            />
            <Label
              for="r-phone"
              class="text-sm font-normal">Phone</Label
            >
          </div>
          <div class="flex items-center gap-2">
            <RadioGroupItem
              value="sms"
              id="r-sms"
            />
            <Label
              for="r-sms"
              class="text-sm font-normal">SMS</Label
            >
          </div>
        </RadioGroup>
      </div>

      <div class="flex flex-col gap-2">
        <Label>Volume ({sliderValue}%)</Label>
        <Slider
          type="single"
          bind:value={sliderValue}
          min={0}
          max={100}
          step={1}
        />
      </div>
    </div>

    <!-- Calendar -->
    <div class="flex flex-col gap-4 rounded-lg border p-6">
      <h3 class="text-muted-foreground text-sm font-medium">Calendar</h3>
      <div class="flex flex-col items-center gap-3">
        <Calendar
          type="single"
          bind:value={calendarValue}
          class="rounded-md border"
        />
        <p class="text-muted-foreground text-sm">
          Selected: {calendarValue ? calendarValue.toString() : "none"}
        </p>
      </div>
    </div>

    <!-- Combobox + Toggle Group -->
    <div class="flex flex-col gap-6 rounded-lg border p-6">
      <!-- Combobox (Command + Popover) -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Combobox</h3>
        <Popover.Root bind:open={comboOpen}>
          <Popover.Trigger>
            <Button
              variant="outline"
              role="combobox"
              class="w-full justify-between"
            >
              {selectedFrameworkLabel}
              <ChevronsUpDownIcon class="ml-2 size-4 shrink-0 opacity-50" />
            </Button>
          </Popover.Trigger>
          <Popover.Content
            class="w-[--radix-popover-trigger-width] p-0"
            align="start"
          >
            <Command.Root>
              <Command.Input placeholder="Search framework..." />
              <Command.List>
                <Command.Empty>No framework found.</Command.Empty>
                <Command.Group>
                  {#each frameworks as framework (framework.value)}
                    <Command.Item
                      value={framework.value}
                      keywords={[framework.label]}
                      onSelect={() => {
                        comboValue = comboValue === framework.value ? "" : framework.value;
                        comboOpen = false;
                      }}
                    >
                      <CheckIcon
                        class="mr-2 size-4 {comboValue === framework.value
                          ? 'opacity-100'
                          : 'opacity-0'}"
                      />
                      {framework.label}
                    </Command.Item>
                  {/each}
                </Command.Group>
              </Command.List>
            </Command.Root>
          </Popover.Content>
        </Popover.Root>
      </div>

      <!-- Toggle Group -->
      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Toggle Group (Single)</h3>
        <ToggleGroup.Root
          type="single"
          bind:value={alignValue}
        >
          <ToggleGroup.Item
            value="left"
            aria-label="Align left"
          >
            <AlignLeftIcon class="size-4" />
          </ToggleGroup.Item>
          <ToggleGroup.Item
            value="center"
            aria-label="Align center"
          >
            <AlignCenterIcon class="size-4" />
          </ToggleGroup.Item>
          <ToggleGroup.Item
            value="right"
            aria-label="Align right"
          >
            <AlignRightIcon class="size-4" />
          </ToggleGroup.Item>
          <ToggleGroup.Item
            value="justify"
            aria-label="Justify"
          >
            <AlignJustifyIcon class="size-4" />
          </ToggleGroup.Item>
        </ToggleGroup.Root>
      </div>

      <div class="flex flex-col gap-3">
        <h3 class="text-muted-foreground text-sm font-medium">Toggle Group (Multi)</h3>
        <ToggleGroup.Root
          type="multiple"
          bind:value={connectivityValues}
        >
          <ToggleGroup.Item
            value="wifi"
            aria-label="Wi-Fi"
          >
            <WifiIcon class="mr-1 size-4" />
            Wi-Fi
          </ToggleGroup.Item>
          <ToggleGroup.Item
            value="bluetooth"
            aria-label="Bluetooth"
          >
            <BluetoothIcon class="mr-1 size-4" />
            Bluetooth
          </ToggleGroup.Item>
          <ToggleGroup.Item
            value="monitor"
            aria-label="Display"
          >
            <MonitorIcon class="mr-1 size-4" />
            Display
          </ToggleGroup.Item>
        </ToggleGroup.Root>
      </div>
    </div>

    <!-- Full form example -->
    <div class="flex flex-col gap-4 rounded-lg border p-6 md:col-span-2">
      <h3 class="text-muted-foreground text-sm font-medium">Combined Form Example</h3>
      <div class="grid gap-4 sm:grid-cols-2">
        <div class="flex flex-col gap-2">
          <Label for="first-name">First name</Label>
          <Input
            id="first-name"
            placeholder="Jane"
          />
        </div>
        <div class="flex flex-col gap-2">
          <Label for="last-name">Last name</Label>
          <Input
            id="last-name"
            placeholder="Doe"
          />
        </div>
      </div>
      <div class="flex flex-col gap-2">
        <Label for="bio">Bio</Label>
        <Textarea
          id="bio"
          placeholder="Tell us about yourself..."
        />
      </div>
      <div class="flex justify-end gap-2">
        <Button variant="outline">Cancel</Button>
        <Button>Save Changes</Button>
      </div>
    </div>
  </div>
</Section>
