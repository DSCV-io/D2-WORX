<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { Select as SelectPrimitive } from "bits-ui";

  let { ref = $bindable(null), ...restProps }: SelectPrimitive.GroupProps = $props();
</script>

<SelectPrimitive.Group
  bind:ref
  data-slot="select-group"
  {...restProps}
/>
