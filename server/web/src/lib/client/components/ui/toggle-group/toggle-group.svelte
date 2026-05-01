<script
  lang="ts"
  module
>
  import { getContext, setContext } from "svelte";
  import type { VariantProps } from "tailwind-variants";
  import { toggleVariants } from "$lib/client/components/ui/toggle/index.js";

  type ToggleVariants = VariantProps<typeof toggleVariants>;

  interface ToggleGroupContext extends ToggleVariants {
    spacing?: number;
  }

  export function setToggleGroupCtx(props: ToggleGroupContext) {
    setContext("toggleGroup", props);
  }

  export function getToggleGroupCtx() {
    return getContext<Required<ToggleGroupContext>>("toggleGroup");
  }
</script>

<script lang="ts">
  import { ToggleGroup as ToggleGroupPrimitive } from "bits-ui";
  import { cn } from "$lib/shared/utils/utils.js";

  let {
    ref = $bindable(null),
    value = $bindable(),
    class: className,
    size = "default",
    spacing = 0,
    variant = "default",
    ...restProps
  }: ToggleGroupPrimitive.RootProps & ToggleVariants & { spacing?: number } = $props();

  let toggleCtx = $state<ToggleGroupContext>({});
  setToggleGroupCtx(toggleCtx);

  // Sync reactive props; $effect.pre runs before DOM update so values are correct on first render
  $effect.pre(() => {
    toggleCtx.variant = variant;
    toggleCtx.size = size;
    toggleCtx.spacing = spacing;
  });
</script>

<!--
Discriminated Unions + Destructing (required for bindable) do not
get along, so we shut typescript up by casting `value` to `never`.
-->
<ToggleGroupPrimitive.Root
  bind:value={value as never}
  bind:ref
  data-slot="toggle-group"
  data-variant={variant}
  data-size={size}
  data-spacing={spacing}
  style={`--gap: ${spacing}`}
  class={cn(
    "group/toggle-group flex w-fit items-center gap-[--spacing(var(--gap))] rounded-md data-[spacing=default]:data-[variant=outline]:shadow-xs",
    className,
  )}
  {...restProps}
/>
