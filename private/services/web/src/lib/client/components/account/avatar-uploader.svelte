<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import * as Avatar from "$lib/client/components/ui/avatar/index.js";
  import * as DropdownMenu from "$lib/client/components/ui/dropdown-menu/index.js";
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
  import PencilIcon from "@lucide/svelte/icons/pencil";
  import UploadIcon from "@lucide/svelte/icons/upload";
  import Trash2Icon from "@lucide/svelte/icons/trash-2";
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

  let uploadState = $state<UploadState>("idle");
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

  onMount(() => {
    const realtime = getRealtimeContext();

    const unsubReady = realtime.on("file:ready", (payload: unknown) => {
      const data = payload as { fileId?: string; variants?: unknown[] };
      if (!data.fileId || data.fileId !== pendingFileId) return;

      invalidateAvatarUrl(data.fileId);
      getAvatarDisplayUrl(data.fileId, "original")
        .then((url) => {
          displayUrl = url;
          uploadState = "idle";
          toast.success(m.webclient_app_account_profile_avatar_success());
          // Auth's handle-file-processed consumer pushes user:updated for
          // user_avatar contextKey → root layout listener handles cache-bust
          // + invalidateAll. The local URL update above gives instant
          // feedback in this component; the cascading refresh hits other
          // avatar consumers (header, sidebar) shortly after via SignalR.
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
      toast.error(
        m.webclient_app_account_profile_avatar_rejected({
          reason: data.rejectionReason ?? m.common_ui_unknown(),
        }),
      );
    });

    return () => {
      unsubReady();
      unsubRejected();
    };
  });

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

  const busy = $derived(
    uploadState === "uploading" || uploadState === "processing" || uploadState === "removing",
  );
  const hasImage = $derived(!!(currentImageFileId || displayUrl));

  function handleFileSelect() {
    fileInput?.click();
  }

  function handleFileChange(e: Event) {
    const input = e.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = "";

    if (!file) return;

    if (!file.type.startsWith("image/")) {
      toast.error(m.webclient_app_account_profile_avatar_invalid_type());
      return;
    }

    if (file.size > MAX_SIZE_BYTES) {
      toast.error(m.webclient_app_account_profile_avatar_too_large());
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
      const msg = err instanceof Error ? err.message : m.common_errors_UNKNOWN();
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
      toast.success(m.webclient_app_account_profile_avatar_success());
    } else {
      uploadState = "idle";
      toast.error(translateMessage(result.messages?.[0], undefined, m.common_errors_UNKNOWN()));
    }
  }
</script>

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

<div class="flex flex-col items-start">
  {#if avatarLoading}
    <Skeleton class="size-32 rounded-full" />
  {:else}
    <DropdownMenu.Root>
      <DropdownMenu.Trigger disabled={busy}>
        {#snippet child({ props })}
          <button
            {...props}
            type="button"
            class="group focus-visible:ring-ring relative block size-32 cursor-pointer rounded-full focus-visible:ring-2 focus-visible:ring-offset-2 focus-visible:outline-none disabled:cursor-not-allowed"
            disabled={busy}
            aria-label={m.webclient_app_account_profile_avatar_edit()}
          >
            {#key displayUrl}
              <Avatar.Root class="size-32 rounded-full">
                {#if displayUrl}
                  <Avatar.Image
                    src={displayUrl}
                    alt={userName ?? m.webclient_app_account_profile_avatar_alt()}
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

            {#if busy}
              <div
                class="absolute inset-0 flex items-center justify-center rounded-full bg-black/50"
              >
                <LoaderIcon class="size-8 animate-spin text-white" />
              </div>

              <div class="absolute inset-0 flex items-center">
                <p class="text-muted-foreground relative -right-[10rem] text-sm">
                  {uploadState === "processing"
                    ? m.webclient_app_account_profile_avatar_processing()
                    : m.webclient_app_account_profile_avatar_uploading()}
                </p>
              </div>
            {:else}
              <span
                class="bg-background text-foreground border-border group-hover:bg-accent absolute bottom-2 left-2 z-10 inline-flex items-center gap-1.5 rounded-md border px-2.5 py-1 text-sm font-medium shadow-sm transition-colors dark:shadow-[0_1px_3px_0_rgb(0_0_0/0.5)]"
              >
                <PencilIcon class="size-4" />
                {m.webclient_app_account_profile_avatar_edit()}
              </span>
            {/if}
          </button>
        {/snippet}
      </DropdownMenu.Trigger>
      <DropdownMenu.Content
        align="start"
        class="w-48"
      >
        <DropdownMenu.Item onSelect={handleFileSelect}>
          <UploadIcon class="mr-2 size-4" />
          {m.webclient_app_account_profile_avatar_change()}
        </DropdownMenu.Item>
        {#if hasImage}
          <DropdownMenu.Separator />
          <DropdownMenu.Item
            variant="destructive"
            onSelect={handleRemove}
          >
            <Trash2Icon class="mr-2 size-4" />
            {m.webclient_app_account_profile_avatar_remove()}
          </DropdownMenu.Item>
        {/if}
      </DropdownMenu.Content>
    </DropdownMenu.Root>
  {/if}
</div>
