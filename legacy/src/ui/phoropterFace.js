// 擬真綜合驗光儀(可操作):拖曳旋鈕轉動、點旋鈕左半邊(−)/右半邊(+)、滑鼠滾輪
import { h } from './dom.js';
import { faceSVG, AUX_POS } from '../render/phoropterSVG.js';
import { VA_ROWS } from '../sim/optics.js';

// 每個旋鈕:stepDeg = 轉幾度算一格;sign = 順時針轉動時值的方向(+1 / −1)
const KNOBS = {
  aux: { stepDeg: 42, sign: -1, apply: (g, e, d) => { const i = Math.max(0, AUX_POS.indexOf(g.phoro[e].aux)); g.setAux(e, AUX_POS[(i + d + AUX_POS.length) % AUX_POS.length]); } },
  coarse: { stepDeg: 26, sign: 1, apply: (g, e, d) => g.stepSph(e, d * 3) },
  // 外側大半圓:手指在輪緣上「往上」= 度數增加(左右兩側旋轉方向相反)
  wheel: { stepDeg: 12, sign: (e) => (e === 'OD' ? 1 : -1), tap: 'y', apply: (g, e, d) => g.stepSph(e, d * 0.25) },
  fine: { stepDeg: 22, sign: -1, apply: (g, e, d) => g.stepSph(e, d * 0.25) },
  cyl: { stepDeg: 18, sign: -1, apply: (g, e, d) => g.stepCyl(e, d * 0.25) }, // 順時針 = 多一格負散光
  axis: { stepDeg: 10, sign: 1, apply: (g, e, d) => g.stepAxis(e, d * 5) },
  axisring: { stepDeg: 10, sign: -1, apply: (g, e, d) => g.stepAxis(e, d * 5) },
  pd: { stepDeg: 14, sign: 1, apply: (g, e, d) => g.setPD(g.phoro.pd + d) },
  level: { stepDeg: 20, sign: 1, apply: (g, e, d) => g.setLevel(g.phoro.level + d * 0.5) },
  vertex: { stepDeg: 30, sign: 1, apply: () => {} },
};
const TAPS = {
  occ: (g, e) => { if (!g.phoro.aperture) g.setAperture(true); else g.setOcc(e, !g.phoro.occ[e]); },
  jcc: (g, e) => {
    if (g.activeEye !== e) g.setActiveEye(e);
    const order = ['off', 'A', 'P'];
    const cur = g.activeEye === e ? g.phoro.jcc.mode : 'off';
    g.setJccMode(order[(order.indexOf(cur) + 1) % 3]);
  },
  jccflip: (g, e) => { if (g.activeEye === e) g.flipJcc(); },
  nearfar: (g) => { g.nearPD = !g.nearPD; g.emit(); },
};

export const faces = new Set();
window.__pfDrag = false;

export class PhoropterFace {
  constructor(game, { zoom = false } = {}) {
    this.game = game;
    this.svgHost = h('div', { class: 'pfsvg' });
    this.ovl = h('div', { class: 'pfovl' });
    this.el = h('div', { class: `pface${zoom ? ' zoom' : ''}`, role: 'group', 'aria-label': '綜合驗光儀,可旋轉旋鈕' },
      h('div', { class: 'pfinner' }, this.svgHost, this.ovl));
    this.drag = null;
    this.retino = null;
    this.rets = {};
    this.bind();
    faces.add(this);
    this.updateNow();
  }

  // 同一個畫面幀內只重繪一次(拖曳時連續多格也不會卡)
  update() {
    if (this._raf) return;
    this._raf = requestAnimationFrame(() => { this._raf = 0; this.render(); });
  }
  updateNow() { cancelAnimationFrame(this._raf); this._raf = 0; this.render(); }
  render() {
    this.svgHost.innerHTML = faceSVG(this.game);
    // 教學模式:要轉的那顆旋鈕發光
    if (this.coachSel) for (const el of this.svgHost.querySelectorAll(this.coachSel)) el.classList.add('coach-hl');
    this.layoutRetino();
  }
  destroy() { faces.delete(this); cancelAnimationFrame(this._raf); this.detachRetino(); }

  // 把檢影畫面(光帶、瞳孔反射)畫進左右兩個窺孔裡:像真的從驗光儀看病人的眼睛
  attachRetino(view) {
    if (this.retino === view) return;
    this.detachRetino();
    this.retino = view;
    for (const eye of ['OD', 'OS']) {
      const c = h('canvas', { class: 'pfret', width: 220, height: 220, 'aria-hidden': 'true' });
      this.rets[eye] = c;
      this.ovl.append(c);
    }
    this._cb = () => {
      // 專注模式蓋在上面時,底下那台不必重畫
      if (document.body.classList.contains('rf-open') && !this.el.closest('.retfs')) return;
      for (const eye of ['OD', 'OS']) if (this.rets[eye]?.style.display !== 'none') view.paintAperture(this.rets[eye], eye);
    };
    view.frameCbs.add(this._cb);
    this.layoutRetino();
  }
  detachRetino() {
    if (this.retino && this._cb) this.retino.frameCbs.delete(this._cb);
    this.retino = null; this._cb = null;
    this.ovl.replaceChildren();
    this.rets = {};
  }
  layoutRetino() {
    if (!this.retino) return;
    const P = this.game.phoro;
    const pdOff = (P.pd - 62) * 1.6;
    for (const eye of ['OD', 'OS']) {
      const c = this.rets[eye];
      if (!c) continue;
      const k = eye === 'OD' ? -1 : 1;
      const ax = 500 + k * (192 + pdOff), ay = 288, r = 73;
      Object.assign(c.style, {
        left: `${((ax - r) / 1000) * 100}%`, top: `${((ay - r) / 680) * 100}%`,
        width: `${((2 * r) / 1000) * 100}%`, height: `${((2 * r) / 680) * 100}%`,
        display: P.occ[eye] || !P.aperture ? 'none' : 'block',
      });
    }
  }

  bind() {
    const el = this.el;
    const find = (t) => t.closest?.('[data-k]');
    el.addEventListener('pointerdown', (e) => {
      const g = find(e.target);
      if (!g) return;
      const k = g.dataset.k, eye = g.dataset.e;
      let cx, cy;
      if (g.dataset.cx) { // 圓心不在元件正中間(外側半圓輪):用 SVG 座標換算
        const svg = g.ownerSVGElement, pt = svg.createSVGPoint();
        pt.x = +g.dataset.cx; pt.y = +g.dataset.cy;
        const p = pt.matrixTransform(svg.getScreenCTM());
        cx = p.x; cy = p.y;
      } else {
        const rect = g.getBoundingClientRect();
        cx = rect.left + rect.width / 2; cy = rect.top + rect.height / 2;
      }
      this.drag = { k, eye, cx, cy, last: Math.atan2(e.clientY - cy, e.clientX - cx), acc: 0, total: 0, x0: e.clientX, y0: e.clientY, moved: false, id: e.pointerId };
      el.setPointerCapture(e.pointerId);
      window.__pfDrag = true;
      e.preventDefault();
    });
    el.addEventListener('pointermove', (e) => {
      const d = this.drag;
      if (!d) return;
      const kn = KNOBS[d.k];
      if (!kn) return;
      let a = Math.atan2(e.clientY - d.cy, e.clientX - d.cx);
      let da = ((a - d.last) * 180) / Math.PI;
      if (da > 180) da -= 360; if (da < -180) da += 360;
      d.last = a;
      d.total += Math.abs(da);
      if (Math.hypot(e.clientX - d.x0, e.clientY - d.y0) > 7 || d.total > 10) d.moved = true;
      d.acc += da;
      if (!d.moved) return;
      while (Math.abs(d.acc) >= kn.stepDeg) {
        const dir = d.acc > 0 ? 1 : -1;
        d.acc -= dir * kn.stepDeg;
        kn.apply(this.game, d.eye, dir * (typeof kn.sign === 'function' ? kn.sign(d.eye) : kn.sign));
      }
    });
    const end = (e) => {
      const d = this.drag;
      if (!d) return;
      this.drag = null;
      window.__pfDrag = false;
      try { el.releasePointerCapture(e.pointerId); } catch { /* ignore */ }
      if (!d.moved) {
        if (TAPS[d.k]) TAPS[d.k](this.game, d.eye);
        else if (KNOBS[d.k]) {
          const dir = (KNOBS[d.k].tap === 'y' ? e.clientY <= d.cy : e.clientX >= d.cx) ? 1 : -1; // 右半邊(輪緣:上半)= 增加
          // 把「值增加」換算成旋鈕的 apply 方向
          const kn = KNOBS[d.k];
          kn.apply(this.game, d.eye, dir * (['cyl'].includes(d.k) ? -1 : 1) * (d.k === 'aux' ? 1 : 1));
        }
      }
      this.game.emit(); // 拖曳期間略過的畫面更新,放開後補上
    };
    el.addEventListener('pointerup', end);
    el.addEventListener('pointercancel', end);
    el.addEventListener('wheel', (e) => {
      const g = find(e.target);
      if (!g || !KNOBS[g.dataset.k]) return;
      e.preventDefault();
      const kn = KNOBS[g.dataset.k];
      kn.apply(this.game, g.dataset.e, (e.deltaY < 0 ? 1 : -1) * (g.dataset.k === 'cyl' ? -1 : 1));
    }, { passive: false });
    el.addEventListener('touchmove', (e) => { if (this.drag) e.preventDefault(); }, { passive: false });
  }
}

// 放大視窗
export function openZoom(game) {
  const face = new PhoropterFace(game, { zoom: true });
  const ov = h('div', { class: 'overlay glass pzoom', onclick: (e) => { if (e.target === ov) close(); } },
    h('div', { class: 'pzoom-in' },
      h('div', { class: 'pzoom-bar' }, h('b', {}, '綜合驗光儀(放大)'), h('span', { class: 'note' }, '拖曳旋鈕轉動;或點旋鈕左半邊(−)/右半邊(+);滑鼠滾輪也可以'),
        h('button', { class: 'b sm', type: 'button', onclick: () => close() }, '✕ 關閉')),
      face.el));
  const off = game.on(() => { if (!window.__pfDrag || true) face.update(); });
  function close() { off(); face.destroy(); ov.remove(); }
  document.body.append(ov);
  return close;
}
export { VA_ROWS };
