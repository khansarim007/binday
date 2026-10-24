// binday landing page — heyclicky-style interactions (vanilla JS, no dependencies)

// ============================================================
// 1. MENUBAR: live clock + battery (heyclicky exact logic)
// ============================================================

const menubar = document.querySelector('.menubar');
if (menubar) {
  // live clock — updates every 30 seconds like heyclicky
  const timeEl = menubar.querySelector('.mb-time');
  if (timeEl) {
    const updateTime = () => {
      timeEl.textContent = new Date().toLocaleTimeString([], { hour: 'numeric', minute: '2-digit', hour12: true });
    };
    updateTime();
    setInterval(updateTime, 30000);
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
        batt.addEventListener('chargingchange', () => {
          if (bolt) bolt.style.opacity = batt.charging ? '1' : '0';
        });
      }).catch(() => {
        // fallback: time-of-day battery simulation (heyclicky's exact formula)
        if (fillRect) {
          const updateBattery = () => {
            const hrs = new Date().getHours();
            let pct;
            const r = hrs + new Date().getMinutes() / 60;
            const daytime = r >= 7 && r < 19;
            if (daytime) {
              pct = Math.max(15, Math.min(100, Math.round(100 - (r - 7) / 12 * 85)));
            } else {
              pct = Math.min(100, Math.max(0, Math.round(15 + (r >= 19 ? r - 19 : r + 5) / 12 * 85)));
            }
            fillRect.style.width = `${pct}%`;
          };
          updateBattery();
          setInterval(updateBattery, 15000);
        }
      });
    }

    // beach ball spinner in menubar (spins on hover like heyclicky)
    const beach = menubar.querySelector('.mb-beach');
    if (beach) {
      beach.addEventListener('mouseenter', () => { beach.style.animationPlayState = 'running'; });
      beach.addEventListener('mouseleave', () => { beach.style.animationPlayState = 'paused'; });
    }
  }
}

// ============================================================
// 2. MARQUEE: spring-phrase track with scroll-velocity physics
// ============================================================

{
  const phrases = ['finds it', 'explains it', 'asks first', 'recycles it', 'restores it', 'nothing lost'];
  const marquee = document.querySelector('.marquee');
  if (!marquee) {
    const m = document.createElement('div');
    m.className = 'marquee';
    m.setAttribute('aria-hidden', 'true');
    m.innerHTML = phrases.map(p => `<span>${p}</span>`).join(' ');
    document.body.appendChild(m);
    marquee = m;
  }
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
  window.addEventListener('scroll', () => {
    vel = (window.scrollY - lastScrollY) * 0.15;
    lastScrollY = window.scrollY;
  }, { passive: true });
  requestAnimationFrame(updateMarquee);
}

// ============================================================
// 3. SCROLL REVEAL: IntersectionObserver (heyclicky .reveal)
// ============================================================

{
  const sections = document.querySelectorAll('.section.reveal');
  const observer = new IntersectionObserver(
    (entries) => {
      entries.forEach((entry) => {
        if (entry.isIntersecting) {
          entry.target.classList.add('in');
          observer.unobserve(entry.target);
        }
      });
    },
    { threshold: 0.12, rootMargin: '-50px 0px -50px' }
  );
  sections.forEach(section => observer.observe(section));
}

// ============================================================
// 4. DRAGGABLE WINDOWS: pointer events on .bar (heyclicky T())
// ============================================================

{
  const wraps = document.querySelectorAll('.abs.win-wrap');
  wraps.forEach(wrap => {
    const bar = wrap.querySelector('.bar');
    if (!wrap || !bar) return;
    wrap.style.position = 'absolute';
    let active = false, sx = 0, sy = 0, lx = wrap.offsetLeft, ly = wrap.offsetTop;

    const onDown = e => {
      e.preventDefault();
      active = true;
      sx = e.clientX;
      sy = e.clientY;
      wrap.style.cursor = 'grabbing';
      bar.style.cursor = 'grabbing';
      wrap.style.userSelect = 'none';
      bar.style.userSelect = 'none';
      document.body.style.userSelect = 'none';
    };

    const onMove = e => {
      if (!active) return;
      lx = Math.max(16, Math.min(window.innerWidth - wrap.offsetWidth - 32, lx + (e.clientX - sx)));
      ly = Math.max(16, Math.min(window.innerHeight - wrap.offsetHeight - 32, ly + (e.clientY - sy)));
      wrap.style.left = lx + 'px';
      wrap.style.top = ly + 'px';
      sx = e.clientX;
      sy = e.clientY;
    };

    const onUp = e => {
      if (active) {
        wrap.style.cursor = 'grab';
        bar.style.cursor = 'grab';
        wrap.style.userSelect = '';
        bar.style.userSelect = '';
        document.body.style.userSelect = '';
      }
      active = false;
    };

    bar.style.touchAction = 'none';
    bar.style.cursor = 'grab';
    bar.addEventListener('pointerdown', onDown);
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
    document.addEventListener('pointercancel', onUp);
  });
}

// ============================================================
// 5. CLICK-TO-ZOOM MODAL: opens on window / screenshot click
// ============================================================

{
  const modal = document.querySelector('.modal');
  if (!modal) return;
  const close = () => { modal.style.display = 'none'; };
  modal.querySelector('.modal-close').addEventListener('click', close);
  modal.querySelector('.modal-backdrop').addEventListener('click', close);
  document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

  const targets = document.querySelectorAll('.win-wrap .win, .hellomov');
  targets.forEach(t => {
    t.style.cursor = 'pointer';
    t.addEventListener('click', () => { modal.style.display = 'flex'; });
  });
}

// ============================================================
// 6. WORD TYPING ROTATOR: "finds it / explains it / ..."
// ============================================================

{
  const words = ['finds the junk.', 'explains it all.', 'asks before it deletes.', 'recycles safely.', 'nothing lost.'];
  const el = document.getElementById('rotate-target');
  if (el) {
    let wi = 0, ci = 0, state = 'type';
    el.className = 'rotator';
    const step = () => {
      if (state === 'type') {
        ci++;
        if (ci > words[wi].length) { state = 'hold'; setTimeout(step, 900); return; }
        el.textContent = words[wi].slice(0, ci);
        setTimeout(step, 40 + 60 * Math.random());
      } else if (state === 'hold') {
        state = 'delete';
        setTimeout(step, 0);
      } else {
        ci--;
        if (ci <= 0) {
          wi = (wi + 1) % words.length;
          state = 'type';
          setTimeout(step, 240);
          return;
        }
        el.textContent = words[wi].slice(0, ci);
        setTimeout(step, 32 + 26 * Math.random());
      }
    };
    setTimeout(step, 650);
  }
}

// ============================================================
// 7. MOUSE PARALLAX on hero decorations (depth-based)
// ============================================================

{
  const deco = document.querySelectorAll('.abs[data-depth]');
  if (deco.length) {
    let mx = 0, my = 0;
    deco.forEach(d => {
      d.setAttribute('data-base-transform', d.style.transform || '');
    });
    const raf = () => {
      deco.forEach(d => {
        const depth = parseFloat(d.getAttribute('data-depth'));
        const dx = (window.innerWidth / 2 - mx) * depth * 0.0008;
        const dy = (window.innerHeight / 2 - my) * depth * 0.0008;
        d.style.transform = d.getAttribute('data-base-transform') + ` translate(${dx}px, ${dy}px)`;
      });
      requestAnimationFrame(raf);
    };
    document.addEventListener('mousemove', e => { mx = e.clientX; my = e.clientY; });
    requestAnimationFrame(raf);
  }
}

// ============================================================
// 8. SMALL HERO VIDEO PLACEHOLDERS: flicker animation
// ============================================================

{
  const style = document.createElement('style');
  style.textContent = `
    .mini-vid::after {
      content: "";
      position: absolute;
      inset: 0;
      background: linear-gradient(90deg, transparent 0%, rgba(255,255,255,0.4) 20%, transparent 40%);
      animation: flicker 3s infinite;
    }
    @keyframes flicker {
      0% { transform: translateX(-100%); }
      100% { transform: translateX(100%); }
    }
  `;
  document.head.appendChild(style);

  document.querySelectorAll('.mini-vid').forEach(v => {
    v.addEventListener('click', e => {
      e.stopPropagation();
      v.classList.toggle('playing');
    });
  });
}
