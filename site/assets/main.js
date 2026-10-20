// BinDay landing page.
//
// The page is deliberately plain: it works with JavaScript disabled, in
// print, and in screenshots. This file only smooths the "download" button
// pulse on first view - nothing that can hide content.

(function () {
  "use strict";

  var download = document.querySelector(".btn-primary");
  if (!download) {
    return;
  }

  // A gentle nudge towards the download button the first time somebody
  // scrolls past the hero, so the primary action is impossible to miss.
  var nudged = false;

  function nudge() {
    if (nudged) {
      return;
    }

    nudged = true;
    download.animate(
      [
        { transform: "translateY(0)" },
        { transform: "translateY(-3px)" },
        { transform: "translateY(0)" }
      ],
      { duration: 420, easing: "ease-out" }
    );

    window.removeEventListener("scroll", nudge);
  }

  if (window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches) {
    return;
  }

  window.addEventListener("scroll", nudge, { passive: true, once: true });
})();
