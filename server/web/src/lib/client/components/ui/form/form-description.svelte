<script lang="ts">
  import * as FormPrimitive from "formsnap";
  import { cn, type WithoutChild } from "$lib/shared/utils/utils.js";

  let {
    ref = $bindable(null),
    class: className,
    ...restProps
  }: WithoutChild<FormPrimitive.DescriptionProps> = $props();
</script>

<FormPrimitive.Description
  bind:ref
  data-slot="form-description"
  class={cn("text-muted-foreground text-xs", className)}
  {...restProps}
/>
