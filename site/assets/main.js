// binday landing page — heyclicky-style interactions (vanilla JS, no dependencies)

// wait for DOM
document.addEventListener('DOMContentLoaded', () => {
  // ==== 1. menubar: live clock + battery ====
  const menubar = document.querySelector('.menubar');
  if (!menubar) return;

  // live clock
  const timeEl = menubar.querySelector('.mb-time');
  if (timeEl) {
    const updateTime = () => {
      const now = new Date();
      timeEl.textContent = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
    };
    updateTime();
    setInterval(updateTime, 1000);
  }

  // battery + charger state
  const batEl = menubar.querySelector('.mb-bat');
  if (batEl) {
    const batSvg = batEl.querySelector('.bat-svg');
    const fillRect = batSvg.querySelector('#bat-fill');
    const bolt = batSvg.querySelector('.bolt');
    if (fillRect && navigator.getBattery) {
      navigator.getBattery().then(batt => {
        const upd = () => {
          const pct = Math.round(batt.level * 100);
          fillRect.style.width = `${pct}%`;
          if (batt.charging) {
            if (bolt) bolt.style.opacity = '1';
            batt.addEventListener('chargingchange', () => {
              if (!batt.charging && bolt) bolt.style.opacity = '0';
            });
          } else if (bolt) bolt.style.opacity = '0';
        };
        upd();
        batt.addEventListener('levelchange', upd);
        batt.addEventListener('chargingchange', upd);
      }).catch(() => {
        // fallback: time-of-day battery simulation
        if (fillRect) {
          const updateBattery = () => {
            const hrs = new Date().getHours();
            let pct;
            if (hrs >= 7 && hrs < 22) {
              // daytime: 100 at 9am, 15% at 6pm
              pct = Math.max(15, 100 - (hrs - 9) * 12.5);
            } else {
              // night: 15 at 10pm, 100 at 6am
              pct = Math.min(100, 15 + (hrs - 22) * 12.5 + 24 * 12.5);
            }
            fillRect.style.width = `${Math.max(0, Math.min(100, pct))}%`;
          };
          updateBattery();
          setInterval(updateBattery, 20000);
        }
      });
    }
  }

  // beach ball spinner in the right side of menubar
  const beach = document.createElement('span');
  beach.className = 'mb-beach';
  beach.setAttribute('aria-hidden', 'true');
  beach.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="10"/><path d="M8 12l2.5-5 2.5 5 5-2.5-5 2.5z"/></svg>';
  const rightSide = menubar.querySelector('.right');
  if (rightSide) rightSide.appendChild(beach);
  if (beach) {
    beach.style.animation = 'beach-spin 2s linear infinite';
    const style = document.createElement('style');
    style.textContent = `
      @keyframes beach-spin { to { transform: rotate(360deg); } }
      .mb-beach { width: 20px; height: 20px; margin-left: 8px; flex-shrink: 0; }
    `;
    document.head.appendChild(style);
  }

  // ==== 2. marquee: spring-phrases track ====
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
  let phraseHTML = phrases.map(p => `<span>${p}</span>`).join(' ');
  marquee.innerHTML = phraseHTML;
  // insert marquee into body (falls back if hero not found yet)
  document.body.appendChild(marquee);

  const track = document.createElement('div');
  track.className = 'marquee-track';
  phrases.forEach(p => {
    const span = document.createElement('span');
    span.textContent = p + ' ';
    track.appendChild(span);
  });
  marquee.appendChild(track);

  // spring state
  let pos = 0;
  let vel = 0;
  const friction = 0.96;
  const dt = 16 / 1000; // ~60fps step

  const updateMarquee = () => {
    vel *= friction;
    pos += vel * 50;
    track.style.transform = `translateX(${ -pos }px)`;
    requestAnimationFrame(updateMarquee);
  };
  // initial velocity from scroll speed
  let lastScrollY = window.scrollY;
  const scrollHandler = () => {
    const delta = window.scrollY - lastScrollY;
    vel = delta * 0.15;
    lastScrollY = window.scrollY;
  };
  window.addEventListener('scroll', scrollHandler, { passive: true });
  requestAnimationFrame(updateMarquee);

  // ==== 3. scroll-dim sections ====
  const sections = document.querySelectorAll('.section:not(.cta-section)');
  const dimFactor = 0.15;
  const translateFactor = 20;

  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry, i) => {
        const section = sections[i];
        if (!section) return;
        if (entry.isIntersecting) {
          section.style.opacity = '';
          section.style.transform = '';
          observer.unobserve(section);
        } else {
          section.style.opacity = dimFactor;
          section.style.transform = `translateY(${translateFactor}px)`;
        }
      });
    },
    { threshold: 0.2, rootMargin: '-50px 0px -50px' }
  );

  sections.forEach(section => observer.observe(section));

  // ==== 4. hero window drag ====
  const wins = document.querySelectorAll('.win-wrap .win');
  wins.forEach(win => {
    const bar = win.querySelector('.bar');
    if (!bar) return;
    let dragging = false;
    let dx = 0, dy = 0;
    let offsetX = win.offsetLeft, offsetY = win.offsetTop;

    const onMouseDown = e => {
      if (e.button !== 0) return;
      dragging = true;
      dx = e.clientX;
      dy = e.clientY;
      win.style.cursor = 'grabbing';
      win.style.userSelect = 'none';
      win.style.transition = 'none';
      document.body.style.userSelect = 'none';
    };

    const onMouseMove = e => {
      if (!dragging) return;
      const dxm = e.clientX - dx;
      const dym = e.clientY - dy;
      // keep within viewport
      const maxX = window.innerWidth - win.offsetWidth - 32;
      const maxY = window.innerHeight - win.offsetHeight - 32;
      offsetX = Math.max(16, Math.min(maxX, offsetX + dxm));
      offsetY = Math.max(16, Math.min(maxY, offsetY + dym));
      win.style.left = `${offsetX}px`;
      win.style.top = `${offsetY}px`;
      dx = e.clientX;
      dy = e.clientY;
    };

    const onMouseUp = e => {
      if (dragging) {
        win.style.cursor = '';
        win.style.userSelect = '';
        document.body.style.userSelect = '';
      }
      dragging = false;
    };

    bar.style.cursor = 'grab';
    bar.addEventListener('mousedown', onMouseDown);
    document.addEventListener('mousemove', onMouseMove);
    document.addEventListener('mouseup', onMouseUp);
  });
});

// keep beach-spin animation available on page