<script lang="ts">
  import * as FormPrimitive from "formsnap";
  import { cn, type WithoutChild } from "$lib/shared/utils/utils.js";

  let {
    ref = $bindable(null),
    children,
    class: className,
    ...restProps
  }: WithoutChild<FormPrimitive.LabelProps> = $props();
</script>

<FormPrimitive.Label
  {...restProps}
  bind:ref
>
  {#snippet child({ props })}
    <label
      {...props}
      data-slot="form-label"
      class={cn(
        "data-[fs-error]:text-destructive flex items-center gap-2 text-sm leading-none font-semibold select-none group-data-[disabled=true]:pointer-events-none group-data-[disabled=true]:opacity-50 peer-disabled:cursor-not-allowed peer-disabled:opacity-50",
        className,
      )}
    >
      {@render children?.()}
    </label>
  {/snippet}
</FormPrimitive.Label>
