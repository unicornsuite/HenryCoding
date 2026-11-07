// Touch-Steuerung für Handys & Tablets
const $ = id => document.getElementById(id);

export const touch = {
  enabled: false,
  moveX: 0, moveY: 0,       // Joystick (-1..1)
  lookDX: 0, lookDY: 0,     // aufsummierte Blickbewegung (Pixel)
  fire: false, aim: false, use: false,
  pressed: new Set(),        // einmalige Aktionen: jump, reload, swap, shop, ready, pause, cancel, nade, melee, use

  init() {
    this.enabled = matchMedia('(pointer: coarse)').matches || ('ontouchstart' in window && navigator.maxTouchPoints > 0) || new URLSearchParams(location.search).has('touch'); // ?touch = Testmodus
    if (!this.enabled) return;
    document.body.classList.add('touch');

    // Schwebender Joystick: erscheint dort, wo der Daumen die linke Bildschirmhälfte berührt
    const zone = $('movezone'), stick = $('stick'), knob = $('knob');
    let sid = null, cx = 0, cy = 0;
    const R = 58;
    zone.addEventListener('touchstart', e => {
      if (sid !== null) return;
      const t = e.changedTouches[0]; sid = t.identifier;
      cx = t.clientX; cy = t.clientY;
      stick.style.left = (cx - 75) + 'px'; stick.style.top = (cy - 75) + 'px'; stick.classList.add('on');
      knob.style.transform = '';
      e.preventDefault();
    }, { passive: false });
    const move = t => {
      let dx = t.clientX - cx, dy = t.clientY - cy;
      const d = Math.hypot(dx, dy);
      if (d > R) { dx *= R / d; dy *= R / d; }
      knob.style.transform = `translate(${dx}px, ${dy}px)`;
      this.moveX = dx / R; this.moveY = dy / R;
      stick.classList.toggle('sprint', this.moveY < -0.92);
    };
    zone.addEventListener('touchmove', e => { for (const t of e.changedTouches) if (t.identifier === sid) move(t); e.preventDefault(); }, { passive: false });
    const endStick = e => { for (const t of e.changedTouches) if (t.identifier === sid) { sid = null; this.moveX = this.moveY = 0; knob.style.transform = ''; stick.classList.remove('on', 'sprint'); } };
    zone.addEventListener('touchend', endStick); zone.addEventListener('touchcancel', endStick);

    // Umsehen (rechte Bildschirmhälfte, unter den Knöpfen)
    const look = $('lookzone');
    const lookIds = new Map();
    look.addEventListener('touchstart', e => { for (const t of e.changedTouches) lookIds.set(t.identifier, [t.clientX, t.clientY]); e.preventDefault(); }, { passive: false });
    look.addEventListener('touchmove', e => {
      for (const t of e.changedTouches) {
        const p = lookIds.get(t.identifier); if (!p) continue;
        this.lookDX += t.clientX - p[0]; this.lookDY += t.clientY - p[1];
        lookIds.set(t.identifier, [t.clientX, t.clientY]);
      }
      e.preventDefault();
    }, { passive: false });
    const endLook = e => { for (const t of e.changedTouches) lookIds.delete(t.identifier); };
    look.addEventListener('touchend', endLook); look.addEventListener('touchcancel', endLook);

    // Feuerknopf: halten + gleichzeitig zielen durch Ziehen
    const fire = $('t-fire'); let fid = null, fp = null;
    fire.addEventListener('touchstart', e => { const t = e.changedTouches[0]; fid = t.identifier; fp = [t.clientX, t.clientY]; this.fire = true; fire.classList.add('on'); e.preventDefault(); }, { passive: false });
    fire.addEventListener('touchmove', e => { for (const t of e.changedTouches) if (t.identifier === fid) { this.lookDX += t.clientX - fp[0]; this.lookDY += t.clientY - fp[1]; fp = [t.clientX, t.clientY]; } e.preventDefault(); }, { passive: false });
    const endFire = () => { this.fire = false; fid = null; fire.classList.remove('on'); };
    fire.addEventListener('touchend', endFire); fire.addEventListener('touchcancel', endFire);

    // Zielen: umschalten
    const aim = $('t-aim');
    aim.addEventListener('touchstart', e => { this.aim = !this.aim; aim.classList.toggle('on', this.aim); e.preventDefault(); }, { passive: false });
    // E: halten (Wiederbeleben) und tippen (Glückskiste)
    const use = $('t-use');
    use.addEventListener('touchstart', e => { this.use = true; this.pressed.add('use'); use.classList.add('on'); e.preventDefault(); }, { passive: false });
    const endUse = () => { this.use = false; use.classList.remove('on'); };
    use.addEventListener('touchend', endUse); use.addEventListener('touchcancel', endUse);

    const tap = (id, action) => {
      const b = $(id);
      b.addEventListener('touchstart', e => { this.pressed.add(action); b.classList.add('on'); e.preventDefault(); }, { passive: false });
      const off = () => b.classList.remove('on');
      b.addEventListener('touchend', off); b.addEventListener('touchcancel', off);
    };
    tap('t-jump', 'jump'); tap('t-reload', 'reload'); tap('t-shop', 'shop'); tap('t-ready', 'ready'); tap('t-pause', 'pause'); tap('t-cancel', 'cancel'); tap('t-nade', 'nade'); tap('t-melee', 'melee');

    // Waffenwechsel: auf die Slots im HUD tippen
    $('slots').addEventListener('touchstart', e => {
      const el = e.target.closest('[data-slot]'); if (!el) return;
      this.pressed.add('slot:' + el.dataset.slot); e.preventDefault();
    }, { passive: false });

    const checkOrient = () => $('rotate').classList.toggle('hidden', !(innerHeight > innerWidth && !$('hud').classList.contains('hidden')));
    addEventListener('resize', checkOrient); this.checkOrient = checkOrient;
  },
  show(on) { if (this.enabled) $('touch').classList.toggle('hidden', !on); if (this.checkOrient) this.checkOrient(); },
  take(action) { const had = this.pressed.has(action); this.pressed.delete(action); return had; },
  /** Gibt den angetippten Waffen-Slot zurück (oder null) */
  takeSlot() { for (const a of this.pressed) if (a.startsWith('slot:')) { this.pressed.delete(a); return a.slice(5); } return null; },
  consumeLook() { const r = [this.lookDX, this.lookDY]; this.lookDX = this.lookDY = 0; return r; },
  setPlacing(on) { if (this.enabled) $('t-cancel').classList.toggle('hidden', !on); },
  vibrate(ms) { try { if (this.enabled && navigator.vibrate) navigator.vibrate(ms); } catch { /* egal */ } },
};
