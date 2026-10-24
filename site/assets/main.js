// binday landing page — heyclicky-style interactions (vanilla JS, no dependencies)

// === menubar: live clock + battery ===
const menubar = document.querySelector('.menubar');
if (menubar) {
  // live clock
  const timeEl = menubar.querySelector('.mb-time');
  if (timeEl) {
    const updateTime = () => { timeEl.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); };
    updateTime();
    setInterval(updateTime, 1000);
  }

  // battery + charger state
  const batEl = menubar.querySelector('.mb-bat');
  if (batEl) {
    const batSvg = batEl.querySelector('.bat-svg');
    const fillRect = batSvg ? batSvg.querySelector('#bat-fill') : null;
    const bolt = batSvg ? batSvg.querySelector('.bolt') : null;
    if (fillRect && navigator.getBattery) {
      navigator.getBattery().then(batt => {
        const upd = () => { fillRect.style.width = `${Math.round(batt.level * 100)}%`; };
        upd();
        batt.addEventListener('levelchange', upd);
        batt.addEventListener('chargingchange', () => { if (bolt) bolt.style.opacity = batt.charging ? '1' : '0'; });
      }).catch(() => {
        // fallback: time-of-day battery simulation
        if (fillRect) {
          const updateBattery = () => {
            const hrs = new Date().getHours();
            let pct;
            if (hrs >= 7 && hrs < 22) { pct = Math.max(15, 100 - (hrs - 9) * 12.5); }
            else { pct = Math.min(100, 15 + (hrs - 22) * 12.5 + 24 * 12.5); }
            fillRect.style.width = `${Math.max(0, Math.min(100, pct))}%`;
          };
          updateBattery();
          setInterval(updateBattery, 20000);
        }
      });
    }
    // beach ball spinner
    const beach = document.createElement('span');
    beach.className = 'mb-beach';
    beach.setAttribute('aria-hidden', 'true');
    beach.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="10"/><path d="M8 12l2.5-5 2.5 5 5-2.5-5 2.5z"/></svg>';
    const rightSide = menubar.querySelector('.right');
    if (rightSide) rightSide.appendChild(beach);
    if (beach) {
      beach.style.animation = 'beach-spin 2s linear infinite';
      const style = document.createElement('style');
      style.textContent = `@keyframes beach-spin { to { transform: rotate(360deg); } }
.mb-beach { width: 20px; height: 20px; margin-left: 8px; flex-shrink: 0; }`;
      document.head.appendChild(style);
    }
  }
}

// === marquee: spring-phrases track ===
(function() {
  const phrases = [
    'finds it',
    'explains it',
    'asks first',
    'recycles it',
    'restores it',
    'nothing lost'
  ];
  const marquee = document.createElement('div');
  marquee.className = 'marquee';
  marquee.setAttribute('aria-hidden', 'true');
  marquee.innerHTML = phrases.map(p => `<span>${p}</span>`).join(' ');
  document.body.appendChild(marquee);

  const track = document.createElement('div');
  track.className = 'marquee-track';
  phrases.forEach(p => {
    const span = document.createElement('span');
    span.textContent = p + ' ';
    track.appendChild(span);
  });
  marquee.appendChild(track);

  let pos = 0, vel = 0, friction = 0.96;
  const updateMarquee = () => {
    vel *= friction;
    pos += vel * 50;
    track.style.transform = `translateX(${ -pos }px)`;
    requestAnimationFrame(updateMarquee);
  };
  let lastScrollY = window.scrollY;
  window.addEventListener('scroll', () => { vel = (window.scrollY - lastScrollY) * 0.15; lastScrollY = window.scrollY; }, { passive: true });
  requestAnimationFrame(updateMarquee);
})();

// === scroll-dim sections ===
(function() {
  const sections = document.querySelectorAll('.section:not(.cta-section)');
  const dimFactor = 0.15, translateFactor = 20;
  const observer = new IntersectionObserver(
    (entries) => { entries.forEach((entry, i) => { const s = sections[i]; if (!s) return; if (entry.isIntersecting) { s.style.opacity = ''; s.style.transform = ''; observer.unobserve(s); } else { s.style.opacity = dimFactor; s.style.transform = `translateY(${translateFactor}px)`; } }); },
    { threshold: 0.2, rootMargin: '-50px 0px -50px' }
  );
  sections.forEach(section => observer.observe(section));
})();

// === hero window drag ===
(function() {
  const wins = document.querySelectorAll('.win-wrap .win');
  wins.forEach(win => {
    const bar = win.querySelector('.bar');
    if (!bar) return;
    let dragging = false, dx = 0, dy = 0, offsetX = win.offsetLeft, offsetY = win.offsetTop;
    const onMouseDown = e => { if (e.button !== 0) return; dragging = true; dx = e.clientX; dy = e.clientY; win.style.cursor = 'grabbing'; win.style.userSelect = 'none'; document.body.style.userSelect = 'none'; };
    const onMouseMove = e => { if (!dragging) return; const dxm = e.clientX - dx, dym = e.clientY - dy; const maxX = window.innerWidth - win.offsetWidth - 32, maxY = window.innerHeight - win.offsetHeight - 32; offsetX = Math.max(16, Math.min(maxX, offsetX + dxm)); offsetY = Math.max(16, Math.min(maxY, offsetY + dym)); win.style.left = `${offsetX}px"; win.style.top = `${offsetY}px"; dx = e.clientX; dy = e.clientY; };
    const onMouseUp = e => { if (dragging) { win.style.cursor = ''; win.style.userSelect = ''; document.body.style.userSelect = ''; } dragging = false; };
    bar.style.cursor = 'grab';
    bar.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  });
})();

// keep beach-spin animation available on page