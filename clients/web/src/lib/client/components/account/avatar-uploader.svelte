<script lang="ts">
  import { invalidateAll } from "$app/navigation";
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import AvatarCropDialog from "./avatar-crop-dialog.svelte";
  import { getRealtimeContext } from "$lib/client/realtime/index.js";
  import { uploadFile } from "$lib/client/rest/files-client.js";
  import { removeAvatar } from "$lib/client/rest/account-client.js";
  import { getAvatarDisplayUrl, invalidateAvatarUrl } from "$lib/client/utils/avatar-url.js";
  import { toast } from "svelte-sonner";
  import { onMount } from "svelte";
  import * as m from "$lib/paraglide/messages.js";
  import { Skeleton } from "$lib/client/components/ui/skeleton/index.js";
  import { translateMessage } from "$lib/client/utils/translate-message.js";
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

  type UploadState = "idle" | "cropping" | "uploading" | "processing" | "ready" | "removing";

  let uploadState: UploadState = $state("idle");
  let selectedFile: File | undefined = $state();
  let cropDialogOpen = $state(false);
  let displayUrl: string | undefined = $state();
  // svelte-ignore state_referenced_locally
  // Initial-only seed so the skeleton renders immediately on SSR when an image
  // is set. The $effect on `currentImageFileId` below keeps it in sync.
  let avatarLoading = $state(!!currentImageFileId);
  let fileInput: HTMLInputElement | undefined = $state();
  let pendingFileId: string | undefined = $state();

  // Client-side limit for the ORIGINAL file (pre-crop). Generous because the
  // cropped output (512x512 WebP) is always well under 1 MB. This just prevents
  // the browser from choking on absurdly large files during Canvas rendering.
  const MAX_SIZE_BYTES = 50 * 1024 * 1024; // 50 MB

  // Resolve avatar URL reactively — clears when image is removed, fetches when set
  $effect(() => {
    if (currentImageFileId) {
      avatarLoading = true;
      getAvatarDisplayUrl(currentImageFileId, "original")
        .then((url) => {
          displayUrl = url;
          avatarLoading = false;
        })
        .catch(() => {
          displayUrl = undefined;
          avatarLoading = false;
        });
    } else {
      displayUrl = undefined;
      avatarLoading = false;
    }
  });

  // Subscribe to SignalR events for file status updates
  onMount(() => {
    const realtime = getRealtimeContext();

    const unsubReady = realtime.on("file:ready", (payload: unknown) => {
      const data = payload as { fileId?: string; variants?: unknown[] };
      if (!data.fileId || data.fileId !== pendingFileId) return;

      invalidateAvatarUrl(data.fileId);
      getAvatarDisplayUrl(data.fileId, "original")
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

  async function handleRemove() {
    uploadState = "removing";
    const result = await removeAvatar();
    if (result.success) {
      displayUrl = undefined;
      pendingFileId = undefined;
      uploadState = "idle";
      toast.success(m.account_profile_avatar_success());
    } else {
      uploadState = "idle";
      toast.error(translateMessage(result.messages?.[0], undefined, m.common_errors_unknown()));
    }
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
<div class="flex flex-col items-center gap-4">
  {#if avatarLoading}
    <Skeleton class="size-32 rounded-full" />
  {:else}
    <button
      type="button"
      class="group relative cursor-pointer"
      onclick={handleFileSelect}
      disabled={uploadState === "uploading" ||
        uploadState === "processing" ||
        uploadState === "removing"}
    >
      {#key displayUrl}
        <Avatar.Root class="size-32 rounded-full">
          {#if displayUrl}
            <Avatar.Image
              src={displayUrl}
              alt={userName ?? m.account_profile_avatar_alt()}
            />
          {/if}
          <Avatar.Fallback
            class="rounded-full text-3xl font-medium text-white"
            style="background-color: {avatarColor()}"
          >
            {initials()}
          </Avatar.Fallback>
        </Avatar.Root>
      {/key}

      <!-- Overlay -->
      {#if uploadState === "removing" || uploadState === "uploading" || uploadState === "processing"}
        <div class="absolute inset-0 flex items-center justify-center rounded-full bg-black/50">
          <LoaderIcon class="size-8 animate-spin text-white" />
        </div>
      {:else}
        <div
          class="absolute inset-0 flex items-center justify-center rounded-full bg-black/0 transition-colors group-hover:bg-black/40"
        >
          <CameraIcon
            class="size-8 text-white opacity-0 transition-opacity group-hover:opacity-100"
          />
        </div>
      {/if}
    </button>
  {/if}

  {#if avatarLoading}
    <Skeleton class="h-5 w-16" />
  {:else if uploadState === "uploading" || uploadState === "processing" || uploadState === "removing"}
    <p class="text-muted-foreground text-sm">
      {uploadState === "uploading"
        ? m.account_profile_avatar_uploading()
        : uploadState === "processing"
          ? m.account_profile_avatar_processing()
          : m.account_profile_avatar_uploading()}
    </p>
  {:else}
    <div class="flex items-center gap-3">
      <button
        type="button"
        class="text-primary cursor-pointer text-sm decoration-[0.1rem] underline-offset-2 hover:underline"
        onclick={handleFileSelect}
      >
        {m.account_profile_avatar_change()}
      </button>
      {#if currentImageFileId || displayUrl}
        <button
          type="button"
          class="text-muted-foreground hover:text-foreground cursor-pointer text-sm decoration-[0.1rem] underline-offset-2 transition-colors hover:underline"
          onclick={handleRemove}
        >
          {m.account_profile_avatar_remove()}
        </button>
      {/if}
    </div>
  {/if}
</div>
