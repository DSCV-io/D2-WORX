<!--
Copyright (c) DCSV. All rights reserved.
-->

<script lang="ts">
  import { createAvatar } from "@dicebear/core";
  import * as identicon from "@dicebear/identicon";

  let {
    seed,
    size = 40,
    class: className = "",
  }: {
    /** Stable hardware/browser fingerprint hash. Same seed → same identicon. */
    seed: string | undefined;
    /** Pixel size of the rendered SVG (square). Defaults to 40. */
    size?: number;
    class?: string;
  } = $props();

  // SVG markup is deterministic from seed — recompute only when seed changes.
  // Returns empty string for missing seeds so the consumer can hide the avatar.
  const svg = $derived(
    seed
      ? createAvatar(identicon, {
          seed,
          size,
          backgroundColor: ["b6e3f4", "c0aede", "d1d4f9", "ffd5dc", "ffdfbf"],
          backgroundType: ["solid"],
          rowColor: ["1e293b"],
        }).toString()
      : "",
  );
</script>

{#if seed}
  <span
    class="inline-block overflow-hidden rounded-md {className}"
    style:width="{size}px"
    style:height="{size}px"
    aria-hidden="true"
    ><!-- DiceBear identicon: SVG generated server-side from a deterministic
         sha256(clientFingerprint) seed. The output contains only geometric
         primitives (<rect>, <path>) — no scripts, no event handlers, no
         user-controlled content flows into the SVG. Suppression is the
         clean path here: the alternative (innerHTML via $effect) bypasses
         the rule mechanically without bypassing the actual risk. -->
    <!-- eslint-disable-next-line svelte/no-at-html-tags -->
    {@html svg}</span
  >
{/if}
