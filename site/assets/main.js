// binday - landing page
// heyclicky-style: scroll reveals, that's it.

document.addEventListener('DOMContentLoaded', function() {
  // mark reveal targets
  document.querySelectorAll('.section, .cta-section').forEach(function(el) {
    el.classList.add('reveal');
  });

  var io = new IntersectionObserver(function(entries) {
    entries.forEach(function(entry) {
      if (entry.isIntersecting) {
        entry.target.classList.add('in');
        io.unobserve(entry.target);
      }
    });
  }, { threshold: 0.12 });

  document.querySelectorAll('.reveal').forEach(function(el) {
    io.observe(el);
  });
});
