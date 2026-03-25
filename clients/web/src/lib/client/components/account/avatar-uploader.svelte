<script lang="ts">
  import { invalidateAll } from "$app/navigation";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import AvatarCropDialog from "./avatar-crop-dialog.svelte";
  import { getRealtimeContext } from "$lib/client/realtime/index.js";
  import { uploadFile } from "$lib/client/rest/files-client.js";
  import { getAvatarDisplayUrl, invalidateAvatarUrl } from "$lib/client/utils/avatar-url.js";
  import { toast } from "svelte-sonner";
  import { onMount } from "svelte";
  import * as m from "$lib/paraglide/messages.js";
  import CameraIcon from "@lucide/svelte/icons/camera";
  import LoaderIcon from "@lucide/svelte/icons/loader-circle";

  let {
    currentImageFileId,
    userId,
    userName,
  }: {
    currentImageFileId?: string;
    userId: string;
    userName?: string;
  } = $props();

  type UploadState = "idle" | "cropping" | "uploading" | "processing" | "ready";

  let uploadState: UploadState = $state("idle");
  let selectedFile: File | undefined = $state();
  let cropDialogOpen = $state(false);
  let displayUrl: string | undefined = $state();
  let fileInput: HTMLInputElement | undefined = $state();
  let pendingFileId: string | undefined = $state();

  // Client-side limit for the ORIGINAL file (pre-crop). Generous because the
  // cropped output (512x512 WebP) is always well under 1 MB. This just prevents
  // the browser from choking on absurdly large files during Canvas rendering.
  const MAX_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB

  // Resolve existing avatar URL on mount
  onMount(() => {
    if (currentImageFileId) {
      getAvatarDisplayUrl(currentImageFileId, "medium")
        .then((url) => {
          displayUrl = url;
        })
        .catch(() => {
          // Silently fail — show initials fallback
        });
    }
  });

  // Subscribe to SignalR events for file status updates
  onMount(() => {
    const realtime = getRealtimeContext();

    const unsubReady = realtime.on("file:ready", (payload: unknown) => {
      const data = payload as { fileId?: string; variants?: unknown[] };
      if (!data.fileId || data.fileId !== pendingFileId) return;

      invalidateAvatarUrl(data.fileId);
      getAvatarDisplayUrl(data.fileId, "medium")
        .then(async (url) => {
          displayUrl = url;
          uploadState = "idle";
          toast.success(m.account_profile_avatar_success());
          await invalidateAll();
        })
        .catch(() => {
          uploadState = "idle";
        });
    });

    const unsubRejected = realtime.on("file:rejected", (payload: unknown) => {
      const data = payload as { fileId?: string; rejectionReason?: string };
      if (!data.fileId || data.fileId !== pendingFileId) return;

      uploadState = "idle";
      pendingFileId = undefined;
      toast.error(m.account_profile_avatar_rejected({ reason: data.rejectionReason ?? "unknown" }));
    });

    return () => {
      unsubReady();
      unsubRejected();
    };
  });

  // Avatar initials + color (same algorithm as UserAvatarMenu)
  const initials = $derived(() => {
    if (!userName) return "?";
    const parts = userName.trim().split(/\s+/);
    if (parts.length >= 2) return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    return parts[0][0]?.toUpperCase() ?? "?";
  });

  const avatarColor = $derived(() => {
    let hash = 0;
    for (const char of userId) {
      hash = (hash * 31 + char.charCodeAt(0)) | 0;
    }
    const hue = ((hash % 360) + 360) % 360;
    return `hsl(${hue}, 55%, 45%)`;
  });

  function handleFileSelect() {
    fileInput?.click();
  }

  function handleFileChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ""; // Reset so same file can be re-selected

    if (!file) return;

    if (!file.type.startsWith("image/")) {
      toast.error(m.account_profile_avatar_invalid_type());
      return;
    }

    if (file.size > MAX_SIZE_BYTES) {
      toast.error(m.account_profile_avatar_too_large());
      return;
    }

    selectedFile = file;
    cropDialogOpen = true;
    uploadState = "cropping";
  }

  function handleCropCancel() {
    if (uploadState === "cropping") uploadState = "idle";
    selectedFile = undefined;
  }

  async function handleCrop(blob: Blob) {
    uploadState = "uploading";

    try {
      const { fileId } = await uploadFile("avatar", blob, selectedFile?.name ?? "avatar.webp");
      pendingFileId = fileId;
      uploadState = "processing";
    } catch (err: unknown) {
      uploadState = "idle";
      const msg = err instanceof Error ? err.message : m.common_errors_unknown();
      toast.error(msg);
    }

    selectedFile = undefined;
  }
</script>

<!-- Hidden file input -->
<input
  bind:this={fileInput}
  type="file"
  accept="image/*"
  class="hidden"
  onchange={handleFileChange}
/>

{#if selectedFile && uploadState === "cropping"}
  <AvatarCropDialog
    imageFile={selectedFile}
    bind:open={cropDialogOpen}
    onCrop={handleCrop}
  />
{/if}

<!-- Avatar display + upload trigger -->
<button
  type="button"
  class="group relative cursor-pointer"
  onclick={handleFileSelect}
  disabled={uploadState === "uploading" || uploadState === "processing"}
>
  <Avatar.Root class="size-24 rounded-full">
    {#if displayUrl}
      <Avatar.Image
        src={displayUrl}
        alt={userName ?? "Avatar"}
      />
    {/if}
    <Avatar.Fallback
      class="rounded-full text-2xl font-medium text-white"
      style="background-color: {avatarColor()}"
    >
      {initials()}
    </Avatar.Fallback>
  </Avatar.Root>

  <!-- Overlay -->
  {#if uploadState === "uploading" || uploadState === "processing"}
    <div class="absolute inset-0 flex items-center justify-center rounded-full bg-black/50">
      <LoaderIcon class="size-6 animate-spin text-white" />
    </div>
  {:else}
    <div
      class="absolute inset-0 flex items-center justify-center rounded-full bg-black/0 transition-colors group-hover:bg-black/40"
    >
      <CameraIcon class="size-6 text-white opacity-0 transition-opacity group-hover:opacity-100" />
    </div>
  {/if}
</button>

{#if uploadState === "uploading" || uploadState === "processing"}
  <p class="text-muted-foreground mt-2 text-xs">
    {uploadState === "uploading"
      ? m.account_profile_avatar_uploading()
      : m.account_profile_avatar_processing()}
  </p>
{/if}
