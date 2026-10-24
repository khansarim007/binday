// binday landing page — heyclicky-style interactions (vanilla JS, no dependencies)

// ==== 1. menubar: live clock + battery ====
const menubar = document.querySelector('.menubar');
if (menubar) {
  const timeEl = menubar.querySelector('.mb-time');
  if (timeEl) {
    const updateTime = () => { timeEl.textContent = new Date().toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }); };
    updateTime();
    setInterval(updateTime, 1000);
  }
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
        if (fillRect) {
          const updateBattery = () => { const hrs = new Date().getHours(); let pct; if (hrs >= 7 && hrs < 22) { pct = Math.max(15, 100 - (hrs - 9) * 12.5); } else { pct = Math.min(100, 15 + (hrs - 22) * 12.5 + 24 * 12.5); } fillRect.style.width = `${Math.max(0, Math.min(100, pct))}%`; };
          updateBattery();
          setInterval(updateBattery, 20000);
        }
      });
    }
    const beach = document.createElement('span');
    beach.className = 'mb-beach';
    beach.setAttribute('aria-hidden', 'true');
    beach.innerHTML = '<svg viewBox="0 0 24 24" fill="currentColor"><circle cx="12" cy="12" r="10"/><path d="M8 12l2.5-5 2.5 5 5-2.5-5 2.5z"/></svg>';
    const rightSide = menubar.querySelector('.right');
    if (rightSide) rightSide.appendChild(beach);
    if (beach) {
      beach.style.animation = 'beach-spin 2s linear infinite';
      const style = document.createElement('style');
      style.textContent = '@keyframes beach-spin { to { transform: rotate(360deg); } }.mb-beach { width: 20px; height: 20px; margin-left: 8px; flex-shrink: 0; }';
      document.head.appendChild(style);
    }
  }
}

// ==== 2. marquee: spring-phrase track ====
{
  const phrases = ['finds it', 'explains it', 'asks first', 'recycles it', 'restores it', 'nothing lost'];
  const marquee = document.querySelector('.marquee');
  if (!marquee) {
    const m = document.createElement('div'); m.className = 'marquee'; m.setAttribute('aria-hidden', 'true'); m.innerHTML = phrases.map(p => `<span>${p}</span>`).join(' '); document.body.appendChild(m); marquee = m;
  }
  const track = document.createElement('div');
  track.className = 'marquee-track';
  phrases.forEach(p => { const span = document.createElement('span'); span.textContent = p + ' '; track.appendChild(span); });
  marquee.appendChild(track);
  let pos = 0, vel = 0, friction = 0.96;
  const updateMarquee = () => { vel *= friction; pos += vel * 50; track.style.transform = `translateX(${ -pos }px)`; requestAnimationFrame(updateMarquee); };
  let lastScrollY = window.scrollY;
  window.addEventListener('scroll', () => { vel = (window.scrollY - lastScrollY) * 0.15; lastScrollY = window.scrollY; }, { passive: true });
  requestAnimationFrame(updateMarquee);
}

// ==== 3. scroll-dim sections ====
{
  const sections = document.querySelectorAll('.section:not(.cta-section)');
  const dimFactor = 0.15, translateFactor = 20;
  const observer = new IntersectionObserver(
    (entries) => { entries.forEach((entry, i) => { const s = sections[i]; if (!s) return; if (entry.isIntersecting) { s.style.opacity = ''; s.style.transform = ''; observer.unobserve(s); } else { s.style.opacity = dimFactor; s.style.transform = `translateY(${translateFactor}px)`; } }); },
    { threshold: 0.2, rootMargin: '-50px 0px -50px' }
  );
  sections.forEach(section => observer.observe(section));
}

// ==== 4. hero window drag ====
{
  const wraps = document.querySelectorAll('.abs.win-wrap');
  wraps.forEach(wrap => {
    const bar = wrap.querySelector('.bar');
    if (!wrap || !bar) return;
    wrap.style.position = 'absolute';
    let active = false, sx = 0, sy = 0, lx = wrap.offsetLeft, ly = wrap.offsetTop;
    const onDown = e => { e.preventDefault(); active = true; sx = e.clientX; sy = e.clientY; wrap.style.cursor = 'grabbing'; bar.style.cursor = 'grabbing'; wrap.style.userSelect = 'none'; bar.style.userSelect = 'none'; document.body.style.userSelect = 'none'; bar.setPointerCapture(e.pointerId); };
    const onMove = e => { if (!active) return; lx = Math.max(16, Math.min(window.innerWidth - wrap.offsetWidth - 32, lx + (e.clientX - sx))); ly = Math.max(16, Math.min(window.innerHeight - wrap.offsetHeight - 32, ly + (e.clientY - sy))); wrap.style.left = lx + 'px'; wrap.style.top = ly + 'px'; sx = e.clientX; sy = e.clientY; };
    const onUp = e => { if (active) { wrap.style.cursor = 'grab'; bar.style.cursor = 'grab'; wrap.style.userSelect = ''; bar.style.userSelect = ''; document.body.style.userSelect = ''; } active = false; };
    bar.style.touchAction = 'none';
    bar.style.cursor = 'grab';
    bar.addEventListener('pointerdown', onDown);
    document.addEventListener('pointermove', onMove);
    document.addEventListener('pointerup', onUp);
    document.addEventListener('pointercancel', onUp);
  });
}

// ==== 5. click-to-zoom on hero windows + screenshot ====
{
  const modal = document.createElement('div');
  modal.className = 'modal';
  modal.setAttribute('role', 'dialog');
  modal.setAttribute('aria-modal', 'true');
  modal.setAttribute('aria-label', 'binday preview');
  modal.innerHTML = '<div class="modal-backdrop"></div><div class="modal-content"><button class="modal-close" role="button" aria-label="close"></button><div class="modal-screen"><div class="mini-card safe"><span class="mini-dot"></span><span class="mini-line w70"></span><span class="mini-tag safe">safe</span></div><div class="mini-card check"><span class="mini-dot"></span><span class="mini-line w50"></span><span class="mini-tag check">check</span></div><div class="mini-card review"><span class="mini-dot"></span><span class="mini-line w80"></span><span class="mini-tag review">review</span></div></div><p class="modal-caption"></p></div>';
  document.body.appendChild(modal);
  const close = () => { modal.style.display = 'none'; };
  modal.querySelector('.modal-close').addEventListener('click', close);
  modal.querySelector('.modal-backdrop').addEventListener('click', close);
  document.addEventListener('keydown', e => { if (e.key === 'Escape') close(); });

  const targets = document.querySelectorAll('.win-wrap .win, .hellomov');
  targets.forEach(t => { t.style.cursor = 'pointer'; t.addEventListener('click', () => { modal.style.display = 'flex'; }); });
}

// ==== 6. word typing rotator ====
{
  const words = ['finds the junk.', 'explains it all.', 'asks before it deletes.', 'recycles safely.', 'nothing lost.'];
  const el = document.getElementById('rotate-target');
  if (el) {
    let wi = 0, ci = 0, state = 'type';
    const step = () => {
      if (state === 'type') { ci++; if (ci > words[wi].length) { state = 'hold'; setTimeout(step, 900); return; } el.textContent = words[wi].slice(0, ci); setTimeout(step, 40 + 60 * Math.random()); }
      else if (state === 'hold') { state = 'delete'; setTimeout(step, 0); }
      else { ci--; if (ci <= 0) { wi = (wi + 1) % words.length; state = 'type'; setTimeout(step, 240); return; } el.textContent = words[wi].slice(0, ci); setTimeout(step, 32 + 26 * Math.random()); }
    };
    el.className = 'rotator';
    setTimeout(step, 650);
  }
}

// ==== 7. mouse parallax on hero decorations ====
{
  const deco = document.querySelectorAll('.abs[data-depth]');
  if (deco.length) {
    let mx = 0, my = 0;
    const raf = () => { deco.forEach(d => { const depth = parseFloat(d.getAttribute('data-depth')); const dx = (window.innerWidth / 2 - mx) * depth * 0.0008; const dy = (window.innerHeight / 2 - my) * depth * 0.0008; d.style.transform = d.getAttribute('data-base-transform') + ` translate(${dx}px, ${dy}px)`; }); requestAnimationFrame(raf); };
    document.addEventListener('mousemove', e => { mx = e.clientX; my = e.clientY; });
    deco.forEach(d => { d.setAttribute('data-base-transform', d.style.transform || ''); });
    requestAnimationFrame(raf);
  }
}