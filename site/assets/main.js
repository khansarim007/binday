// BinDay landing page - progressive enhancement only.
// The page works fine with JavaScript disabled; this just adds a soft reveal.

(function () {
  "use strict";

  var reduced = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  if (reduced || !("IntersectionObserver" in window)) {
    return;
  }

  var targets = document.querySelectorAll(".card, .grade, .step, .cta");
  if (!targets.length) {
    return;
  }

  for (var i = 0; i < targets.length; i++) {
    targets[i].classList.add("reveal");
  }

  var observer = new IntersectionObserver(function (entries) {
    for (var i = 0; i < entries.length; i++) {
      if (entries[i].isIntersecting) {
        entries[i].target.classList.add("in");
        observer.unobserve(entries[i].target);
      }
    }
  }, { rootMargin: "0px 0px -8% 0px" });

  for (var j = 0; j < targets.length; j++) {
    observer.observe(targets[j]);
  }
})();
