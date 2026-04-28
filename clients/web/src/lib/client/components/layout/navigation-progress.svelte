<script lang="ts">
  import { navigating } from "$app/stores";
  import * as m from "$lib/paraglide/messages.js";

  let visible = $state(false);
  let completing = $state(false);
  let delayTimer: ReturnType<typeof setTimeout> | undefined;
  let hideTimer: ReturnType<typeof setTimeout> | undefined;

  $effect(() => {
    const nav = $navigating;

    if (nav) {
      // Navigation started — show bar after a short delay to avoid flash on instant navs
      completing = false;
      clearTimeout(hideTimer);
      delayTimer = setTimeout(() => {
        visible = true;
      }, 150);
    } else if (visible) {
      // Navigation finished while bar is visible — animate to 100% then fade out
      clearTimeout(delayTimer);
      completing = true;
      hideTimer = setTimeout(() => {
        visible = false;
        completing = false;
      }, 500);
    } else {
      // Navigation finished before bar appeared — clean up
      clearTimeout(delayTimer);
    }

    return () => {
      clearTimeout(delayTimer);
      clearTimeout(hideTimer);
    };
  });
</script>

{#if visible}
  <div
    class="fixed top-0 right-0 left-0 z-[9999] h-1"
    role="progressbar"
    aria-label={m.common_ui_loading_page()}
  >
    <div
      class="bg-primary h-full origin-left"
      class:bar-loading={!completing}
      class:bar-complete={completing}
    ></div>
  </div>
{/if}

<style>
  @keyframes loading {
    0% {
      transform: scaleX(0);
    }
    20% {
      transform: scaleX(0.3);
    }
    50% {
      transform: scaleX(0.6);
    }
    80% {
      transform: scaleX(0.8);
    }
    100% {
      transform: scaleX(0.95);
    }
  }

  .bar-loading {
    animation: loading 8s cubic-bezier(0.4, 0, 0, 1) forwards;
  }

  .bar-complete {
    transform: scaleX(1);
    opacity: 0;
    transition:
      transform 200ms ease-out,
      opacity 400ms ease-out 200ms;
  }
</style>
