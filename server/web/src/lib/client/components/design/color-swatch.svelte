<script lang="ts">
  let { name, value }: { name: string; value: string } = $props();
</script>

<div class="flex flex-col gap-1.5">
  <div
    class="border-border h-12 w-full rounded-md border"
    style="background: {value}"
  ></div>
  <div class="flex flex-col">
    <span class="text-xs font-medium">{name}</span>
    <span class="text-muted-foreground font-mono text-[10px]">{value}</span>
  </div>
</div>
