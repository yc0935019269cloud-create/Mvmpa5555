// 擬真綜合驗光儀(可操作):拖曳旋鈕轉動、點旋鈕左半邊(−)/右半邊(+)、滑鼠滾輪
import { h } from './dom.js';
import { faceSVG, AUX_POS } from '../render/phoropterSVG.js';
import { VA_ROWS } from '../sim/optics.js';

// 每個旋鈕:stepDeg = 轉幾度算一格;sign = 順時針轉動時值的方向(+1 / −1)
const KNOBS = {
  aux: { stepDeg: 42, sign: -1, apply: (g, e, d) => { const i = Math.max(0, AUX_POS.indexOf(g.phoro[e].aux)); g.setAux(e, AUX_POS[(i + d + AUX_POS.length) % AUX_POS.length]); } },
  coarse: { stepDeg: 26, sign: 1, apply: (g, e, d) => g.stepSph(e, d * 3) },
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
    this.el = h('div', { class: `pface${zoom ? ' zoom' : ''}`, role: 'group', 'aria-label': '綜合驗光儀,可旋轉旋鈕' });
    this.drag = null;
    this.bind();
    faces.add(this);
    this.update();
  }

  update() { this.el.innerHTML = faceSVG(this.game); }
  destroy() { faces.delete(this); }

  bind() {
    const el = this.el;
    const find = (t) => t.closest?.('[data-k]');
    el.addEventListener('pointerdown', (e) => {
      const g = find(e.target);
      if (!g) return;
      const k = g.dataset.k, eye = g.dataset.e;
      const rect = g.getBoundingClientRect();
      const cx = rect.left + rect.width / 2, cy = rect.top + rect.height / 2;
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
        kn.apply(this.game, d.eye, dir * kn.sign);
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
          const dir = e.clientX >= d.cx ? 1 : -1; // 右半邊 = 值增加,左半邊 = 值減少
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
