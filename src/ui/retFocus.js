// 檢影「專注模式」:全螢幕、單手可操作(手機直放 / 橫放各有版面)
import { h, btn, holdRepeat, segmented } from './dom.js';
import { RetinoView } from './retinoCanvas.js';
import { EYES, EYE_LABEL } from '../game/state.js';
import { WD_OPTIONS } from '../sim/retino.js';
import { fmtRx, fmtSph } from '../sim/optics.js';

const pad3 = (n) => String(Math.round(n)).padStart(3, '0');

// 圓形角度盤:拖曳一圈就是轉光條(0–180°)
function angleDial(view, onChange) {
  const R = 54, C = 64;
  const el = h('div', { class: 'adial', role: 'slider', 'aria-label': '光條角度', 'aria-valuemin': 1, 'aria-valuemax': 180 });
  const draw = () => {
    const a = (view.angle * Math.PI) / 180;
    const x = Math.cos(a) * R, y = -Math.sin(a) * R;
    let ticks = '';
    for (let d = 0; d < 360; d += 15) {
      const r = (d * Math.PI) / 180, major = d % 45 === 0;
      ticks += `<line x1="${C + Math.cos(r) * (R - (major ? 9 : 5))}" y1="${C - Math.sin(r) * (R - (major ? 9 : 5))}" x2="${C + Math.cos(r) * R}" y2="${C - Math.sin(r) * R}" stroke="#7d95a0" stroke-width="${major ? 2.2 : 1.2}"/>`;
    }
    el.innerHTML = `<svg viewBox="0 0 128 128" width="128" height="128"><circle cx="${C}" cy="${C}" r="${R + 6}" fill="#13212a" stroke="#3b525d" stroke-width="2"/>${ticks}<line x1="${C - x}" y1="${C - y}" x2="${C + x}" y2="${C + y}" stroke="#ffd37a" stroke-width="6" stroke-linecap="round"/><circle cx="${C + x}" cy="${C + y}" r="7" fill="#ffb703"/><text x="${C}" y="${C + 6}" text-anchor="middle" font-size="19" font-weight="700" fill="#e8f1f4" font-family="'IBM Plex Mono',monospace">${pad3(view.angle)}°</text></svg>`;
    el.setAttribute('aria-valuenow', view.angle);
  };
  let drag = false;
  const set = (e) => {
    const r = el.getBoundingClientRect();
    const dx = e.clientX - (r.left + r.width / 2), dy = e.clientY - (r.top + r.height / 2);
    let deg = (-Math.atan2(dy, dx) * 180) / Math.PI;
    deg = ((deg % 180) + 180) % 180;
    view.angle = Math.round(deg / 2) * 2 || 180; // 2° 一格,好抓
    draw(); onChange?.();
  };
  el.addEventListener('pointerdown', (e) => { drag = true; el.setPointerCapture(e.pointerId); set(e); e.preventDefault(); });
  el.addEventListener('pointermove', (e) => { if (drag) set(e); });
  const up = () => { drag = false; };
  el.addEventListener('pointerup', up); el.addEventListener('pointercancel', up);
  el.addEventListener('touchmove', (e) => e.preventDefault(), { passive: false });
  draw();
  return { el, draw };
}

export function openRetFocus(game, ctx, { onClose } = {}) {
  const canvas = h('canvas', { class: 'retcv big', 'aria-label': '檢影:瞳孔內的反射光' });
  const view = new RetinoView(canvas, game);
  view.eye = game.activeEye;
  const stops = [];

  // --- 鏡片列(固定元件,只更新文字,不重建 → 長按連續調整不會被打斷)
  const refs = {};
  const row = (label, key, steps, apply) => {
    const out = h('output', { class: 'val' }, '');
    refs[key] = out;
    return h('div', { class: 'lrow' },
      h('span', { class: 'lab' }, label),
      steps.filter((s) => s[1] < 0).map(([l, d]) => mk(l, () => apply(d))),
      out,
      steps.filter((s) => s[1] > 0).map(([l, d]) => mk(l, () => apply(d))));
  };
  const mk = (label, fn) => {
    const b = btn(label, fn, { cls: 'big' });
    stops.push(holdRepeat(b, fn));
    return b;
  };
  const eyeLens = () => game.phoro[view.eye];
  const rows = [
    row('球面', 'sph', [['−1', -1], ['−.25', -0.25], ['+.25', 0.25], ['+1', 1]], (d) => game.stepSph(view.eye, d)),
    row('散光', 'cyl', [['−.25', -0.25], ['+.25', 0.25]], (d) => game.stepCyl(view.eye, d)),
    row('軸度', 'axis', [['−15', -15], ['−5', -5], ['+5', 5], ['+15', 15]], (d) => game.stepAxis(view.eye, d)),
  ];
  const rxOut = h('b', { class: 'rx big' }, '');
  const eyeSeg = h('div', { class: 'seg' });
  const wdSel = h('select', { 'aria-label': '工作距離', onchange: (e) => game.setWD(+e.target.value) },
    WD_OPTIONS.map((o) => h('option', { value: o.cm }, `${o.cm} cm`)));
  const dial = angleDial(view, () => {});
  const autoBtn = btn('', () => { view.auto = !view.auto; sync(); }, { cls: 'big' });
  const slowBtn = btn('', () => { view.slow = !view.slow; sync(); }, { cls: 'big' });
  const hintBtn = btn('', () => { view.hintOn = !view.hintOn; sync(); }, { cls: 'big hintb' });
  const bottom = h('div', { class: 'rfbar' });
  const tip = h('p', { class: 'rftip' }, '在畫面上左右拖曳 = 掃動光條;轉動圓盤 = 轉光條角度。順動加正、逆動減正,雙軸都中和(整個瞳孔亮起)就完成。');

  const root = h('div', { class: 'retfs', role: 'dialog', 'aria-label': '檢影專注模式' },
    h('div', { class: 'rfhead' },
      h('button', { class: 'b big', type: 'button', onclick: () => close(true) }, '✕ 返回'),
      eyeSeg, h('span', { class: 'sp' }), hintBtn, wdSel),
    h('div', { class: 'rfcanvas' }, canvas),
    h('div', { class: 'rfctl' },
      h('div', { class: 'rxrow' }, rxOut),
      h('div', { class: 'angrow' }, dial.el,
        h('div', { class: 'angbtns' },
          h('div', { class: 'row' }, mk('−15°', () => setAngle(view.angle - 15)), mk('−2°', () => setAngle(view.angle - 2))),
          h('div', { class: 'row' }, mk('+2°', () => setAngle(view.angle + 2)), mk('+15°', () => setAngle(view.angle + 15))),
          h('div', { class: 'row' }, autoBtn, slowBtn))),
      ...rows, tip),
    bottom);
  function setAngle(a) { view.angle = ((Math.round(a) - 1 + 180) % 180) + 1; dial.draw(); }

  function renderBottom() {
    const stepId = game.stepId;
    const acts = [];
    const c = game.canAdvance();
    if (stepId === 'ret') {
      acts.push(btn('完成檢影 ▶ 進工作距離', () => finish(), { cls: 'primary big wide' }));
    } else {
      acts.push(
        btn(`進工作距離 −${game.wdD.toFixed(2)}D`, () => game.applyWD(), { cls: 'big', disabled: game.wdApplied }),
        btn('記錄 OD', () => { game.recordRx('ret', 'OD'); ctx.toast('已記錄 OD'); }, { cls: 'big' }),
        btn('記錄 OS', () => { game.recordRx('ret', 'OS'); ctx.toast('已記錄 OS'); }, { cls: 'big' }),
        btn(`記錄 ${game.wdCm} cm`, () => { game.recordWD(game.wdCm); ctx.toast('已記錄工作距離'); }, { cls: 'big' }));
      if (stepId === 'wd') acts.push(btn('完成 ▶ 測 VA', () => finish(), { cls: 'primary big wide' }));
    }
    void c;
    bottom.replaceChildren(...acts);
  }

  function finish() {
    const c = game.canAdvance();
    if (!c.ok) { ctx.toast(c.why[0] ?? '還沒達成條件', true); return; }
    game.advance();
    if (game.stepId !== 'wd') close(false); // 完成 wd 之後離開專注模式
  }

  function sync() {
    const g = game, L = eyeLens();
    rxOut.textContent = `${view.eye}  ${fmtRx(L)}`;
    refs.sph.textContent = fmtSph(L.s);
    refs.cyl.textContent = L.c === 0 ? '0' : L.c.toFixed(2);
    refs.axis.textContent = `${pad3(L.a)}°`;
    wdSel.value = String(g.wdCm);
    autoBtn.textContent = view.auto ? '⏸ 暫停掃動' : '▶ 自動掃動';
    slowBtn.textContent = view.slow ? '慢動作 ✓' : '慢動作';
    hintBtn.textContent = view.hintOn ? '💡 ✓' : '💡';
    eyeSeg.replaceChildren(...EYES.map((e) => h('button', { type: 'button', class: `s${view.eye === e ? ' on' : ''}`, onclick: () => { view.eye = e; g.setActiveEye(e); sync(); } }, e === 'OD' ? 'OD 右' : 'OS 左')));
    renderBottom();
  }

  function close(manual) {
    stops.forEach((s) => s());
    view.stop(); off(); root.remove();
    document.body.classList.remove('rf-open');
    onClose?.(manual);
  }

  const off = game.on((k) => { if (k !== 'log') sync(); });
  document.body.append(root);
  document.body.classList.add('rf-open');
  sync();
  view.start();
  return { close, view };
}
