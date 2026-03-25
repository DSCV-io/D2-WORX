<script lang="ts">
  import * as Dialog from "$lib/client/components/ui/dialog/index.js";
  import { Button } from "$lib/client/components/ui/button/index.js";
  import * as m from "$lib/paraglide/messages.js";
  import { onMount } from "svelte";

  let {
    imageFile,
    open = $bindable(false),
    onCrop,
  }: {
    imageFile: File;
    open?: boolean;
    onCrop: (blob: Blob) => void;
  } = $props();

  const OUTPUT_SIZE = 512;
  let canvas: HTMLCanvasElement | undefined = $state();
  let img: HTMLImageElement | undefined = $state();
  let zoom = $state(1);
  let panX = $state(0);
  let panY = $state(0);
  let dragging = $state(false);
  let dragStartX = 0;
  let dragStartY = 0;
  let panStartX = 0;
  let panStartY = 0;

  // Load image when file changes
  $effect(() => {
    if (!imageFile) return;
    const newImg = new Image();
    newImg.onload = () => {
      img = newImg;
      zoom = 1;
      panX = 0;
      panY = 0;
    };
    newImg.src = URL.createObjectURL(imageFile);
    return () => URL.revokeObjectURL(newImg.src);
  });

  // Redraw canvas when state changes
  $effect(() => {
    if (!canvas || !img) return;
    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const size = canvas.width;
    const radius = size / 2;

    ctx.clearRect(0, 0, size, size);

    // Draw the image scaled and panned
    const scale = Math.min(size / img.width, size / img.height) * zoom;
    const drawW = img.width * scale;
    const drawH = img.height * scale;
    const drawX = (size - drawW) / 2 + panX;
    const drawY = (size - drawH) / 2 + panY;

    ctx.save();
    ctx.drawImage(img, drawX, drawY, drawW, drawH);
    ctx.restore();

    // Draw dark overlay with circle cutout using evenodd fill rule.
    // Outer rect (full canvas) + inner circle = only the area outside the circle is filled.
    ctx.save();
    ctx.fillStyle = "rgba(0, 0, 0, 0.6)";
    ctx.beginPath();
    ctx.rect(0, 0, size, size);
    ctx.arc(radius, radius, radius - 4, 0, Math.PI * 2, true);
    ctx.fill("evenodd");
    ctx.restore();

    // Draw circle border
    ctx.strokeStyle = "rgba(255, 255, 255, 0.3)";
    ctx.lineWidth = 2;
    ctx.beginPath();
    ctx.arc(radius, radius, radius - 4, 0, Math.PI * 2);
    ctx.stroke();
  });

  function handlePointerDown(e: PointerEvent) {
    dragging = true;
    dragStartX = e.clientX;
    dragStartY = e.clientY;
    panStartX = panX;
    panStartY = panY;
    (e.currentTarget as HTMLElement).setPointerCapture(e.pointerId);
  }

  function handlePointerMove(e: PointerEvent) {
    if (!dragging) return;
    panX = panStartX + (e.clientX - dragStartX);
    panY = panStartY + (e.clientY - dragStartY);
  }

  function handlePointerUp() {
    dragging = false;
  }

  function handleWheel(e: WheelEvent) {
    e.preventDefault();
    const delta = e.deltaY > 0 ? -0.1 : 0.1;
    zoom = Math.max(0.5, Math.min(5, zoom + delta));
  }

  function handleZoomInput(e: Event) {
    zoom = parseFloat((e.target as HTMLInputElement).value);
  }

  async function handleCrop() {
    if (!canvas || !img) return;

    // Render the visible circle to a clean output canvas
    const output = document.createElement("canvas");
    output.width = OUTPUT_SIZE;
    output.height = OUTPUT_SIZE;
    const ctx = output.getContext("2d");
    if (!ctx) return;

    const size = canvas.width;
    const scale = Math.min(size / img.width, size / img.height) * zoom;
    const drawW = img.width * scale;
    const drawH = img.height * scale;
    const drawX = (size - drawW) / 2 + panX;
    const drawY = (size - drawH) / 2 + panY;

    // Scale from preview canvas to output size
    const outputScale = OUTPUT_SIZE / size;

    // Clip to circle
    ctx.beginPath();
    ctx.arc(OUTPUT_SIZE / 2, OUTPUT_SIZE / 2, OUTPUT_SIZE / 2, 0, Math.PI * 2);
    ctx.clip();

    ctx.drawImage(
      img,
      drawX * outputScale,
      drawY * outputScale,
      drawW * outputScale,
      drawH * outputScale,
    );

    output.toBlob(
      (blob) => {
        if (blob) {
          onCrop(blob);
          open = false;
        }
      },
      "image/webp",
      0.9,
    );
  }
</script>

<Dialog.Root bind:open>
  <Dialog.Content class="max-w-sm">
    <Dialog.Header>
      <Dialog.Title>{m.account_profile_avatar_crop_title()}</Dialog.Title>
      <Dialog.Description>{m.account_profile_avatar_crop_description()}</Dialog.Description>
    </Dialog.Header>

    <div class="flex flex-col items-center gap-4 py-4">
      <!-- Canvas preview -->
      <canvas
        bind:this={canvas}
        width={280}
        height={280}
        class="cursor-grab rounded-full active:cursor-grabbing"
        onpointerdown={handlePointerDown}
        onpointermove={handlePointerMove}
        onpointerup={handlePointerUp}
        onwheel={handleWheel}
      ></canvas>

      <!-- Zoom slider -->
      <div class="flex w-full items-center gap-3 px-4">
        <span class="text-muted-foreground text-xs">-</span>
        <input
          type="range"
          min="0.5"
          max="5"
          step="0.05"
          value={zoom}
          oninput={handleZoomInput}
          class="w-full"
        />
        <span class="text-muted-foreground text-xs">+</span>
      </div>
    </div>

    <Dialog.Footer>
      <Button
        variant="ghost"
        onclick={() => (open = false)}>{m.common_ui_cancel()}</Button
      >
      <Button onclick={handleCrop}>{m.account_profile_avatar_crop_confirm()}</Button>
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
