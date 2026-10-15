// 檢影畫面:暗室裡一條光帶掃過眼睛,瞳孔內的反射光會「順動 / 逆動 / 中和」
// 同一份狀態(光條角度、掃動位置)可以畫在大畫面,也可以畫進綜合驗光儀的窺孔裡。
import { reflex } from '../sim/retino.js';
import { rad } from '../sim/optics.js';

const W = 520, H = 330;

export class RetinoView {
  constructor(canvas, game) {
    this.cv = canvas ?? document.createElement('canvas');
    this.cv.width = W; this.cv.height = H;
    this.ctx = this.cv.getContext('2d');
    this.game = game;
    this.eye = 'OD';
    this.angle = 90; // 光條線的方向(度)
    this.auto = true;
    this.slow = false;
    this.hintOn = game.mode !== 'exam'; // 顯示「順動/逆動」判讀(教學用)
    this.manualS = null;
    this.t = 0;
    this.s = 0; // 光帶目前的掃動位置(-1.1 ~ 1.1)
    this.last = performance.now();
    this.info = null;
    this.running = false;
    this.frameCbs = new Set(); // 每一幀畫完後通知(驗光儀窺孔跟著重畫)
    this.iris = ['#6b4a2f', '#4f7a8a', '#5d6f3f', '#7a5a3a'][game.patient.seed % 4];
    // 虹膜紋理(固定,每位受測者一樣)
    this.striae = Array.from({ length: 64 }, (_, i) => ({ a: (i / 64) * Math.PI * 2 + Math.sin(i * 12.9898 + game.patient.seed) * 0.05, l: 0.55 + ((Math.sin(i * 78.233 + game.patient.seed) * 43758.5453) % 1 + 1) % 1 * 0.45, d: i % 3 === 0 }));
    this.blink = 0;
    this.blinkAt = performance.now() + 2500 + Math.random() * 3000;
    if (canvas) this.bindPointer();
  }

  bindPointer() {
    const c = this.cv;
    const pos = (e) => {
      const r = c.getBoundingClientRect();
      return { x: ((e.clientX - r.left) / r.width) * W, y: ((e.clientY - r.top) / r.height) * H };
    };
    const setS = (e) => {
      const p = pos(e);
      const phi = rad(this.angle + 90);
      const ux = Math.cos(phi), uy = -Math.sin(phi);
      const s = ((p.x - W / 2) * ux + (p.y - H / 2) * uy) / 125;
      this.manualS = Math.max(-1.1, Math.min(1.1, s));
    };
    c.addEventListener('pointerdown', (e) => { c.setPointerCapture(e.pointerId); setS(e); });
    c.addEventListener('pointermove', (e) => { if (this.manualS !== null) setS(e); });
    const end = () => { this.manualS = null; };
    c.addEventListener('pointerup', end); c.addEventListener('pointercancel', end);
  }

  start() {
    if (this.running) return;
    this.running = true;
    this.last = performance.now();
    const loop = (now) => {
      if (!this.running) return;
      const dt = Math.min(0.05, (now - this.last) / 1000);
      this.last = now;
      if (this.auto || this.manualS !== null) this.t += dt * (this.slow ? 0.3 : 1);
      this.draw();
      this.raf = requestAnimationFrame(loop);
    };
    this.raf = requestAnimationFrame(loop);
  }
  stop() { this.running = false; cancelAnimationFrame(this.raf); }

  // 一幀:更新掃動位置 → 畫大畫面 → 通知窺孔重畫
  draw() {
    const g = this.game;
    // 偶爾眨眼(約 180 ms)
    const now = performance.now();
    if (now > this.blinkAt + 180) this.blinkAt = now + 3000 + Math.random() * 4000;
    const bp = (now - this.blinkAt) / 180;
    this.blink = bp > 0 && bp < 1 ? 1 - Math.abs(bp * 2 - 1) : 0;
    this.s = this.manualS !== null ? this.manualS : Math.sin(this.t * 2 * Math.PI * 0.5) * 1.05;
    this.info = reflex(g.trueRx(this.eye), g.lens(this.eye), g.wdD, this.angle);
    this.scene(this.ctx, this.eye, { overlay: true, streak: true });
    for (const cb of this.frameCbs) cb();
  }

  // 畫進驗光儀窺孔(方形 canvas,外觀由 CSS 裁成圓形)
  paintAperture(canvas, eye) {
    const size = canvas.width;
    const ctx = canvas.getContext('2d');
    ctx.setTransform(1, 0, 0, 1, 0, 0);
    ctx.fillStyle = '#000'; ctx.fillRect(0, 0, size, size);
    const f = size / 250; // 只取眼睛中央約 250 單位寬(虹膜 + 瞳孔)
    ctx.setTransform(f, 0, 0, f, size / 2 - (W / 2) * f, size / 2 - (H / 2) * f);
    this.scene(ctx, eye, { overlay: false, streak: eye === this.eye });
    ctx.setTransform(1, 0, 0, 1, 0, 0);
  }

  // 畫一隻眼睛的檢影場景。streak=false 時只畫靜止的眼睛(沒有被檢影的那一眼)
  scene(ctx, eye, { overlay = true, streak = true } = {}) {
    const g = this.game;
    const info = streak ? (eye === this.eye && this.info ? this.info : reflex(g.trueRx(eye), g.lens(eye), g.wdD, this.angle)) : null;
    const cx = W / 2, cy = H / 2;
    const s = this.s;
    const phi = rad(this.angle + 90);
    const th = rad(this.angle);
    const ux = Math.cos(phi), uy = -Math.sin(phi);
    const Rb = 125;
    // 室內燈光:調暗 → 瞳孔放大、反射光對比清楚;燈太亮 → 瞳孔縮小、反射光被洗淡(前置要調暗燈光的原因)
    const dim = g.room.dim;
    const pupilR = (dim ? 38 : 26) * (1 + Math.sin(performance.now() / 900) * 0.015);
    const contrast = dim ? 1 : 0.55;

    ctx.fillStyle = '#080b0d'; ctx.fillRect(0, 0, W, H);
    // 臉 / 眼白
    ctx.save();
    const grd = ctx.createRadialGradient(cx, cy, 20, cx, cy, 190);
    grd.addColorStop(0, dim ? '#3a2a24' : '#7a5f52'); grd.addColorStop(1, dim ? '#161110' : '#3b2d27');
    ctx.fillStyle = grd; ctx.fillRect(0, 0, W, H);
    ctx.beginPath(); ctx.ellipse(cx, cy, 190, 92, 0, 0, Math.PI * 2);
    ctx.fillStyle = dim ? '#4a403d' : '#a39890'; ctx.fill();
    // 虹膜(放射狀紋理 + 角膜緣)與瞳孔
    ctx.beginPath(); ctx.arc(cx, cy, 78, 0, Math.PI * 2); ctx.fillStyle = this.iris; ctx.globalAlpha = dim ? 0.55 : 0.85; ctx.fill();
    ctx.lineWidth = 1.4;
    for (const st of this.striae) {
      ctx.strokeStyle = st.d ? 'rgba(0,0,0,.35)' : 'rgba(255,240,220,.16)';
      ctx.beginPath();
      ctx.moveTo(cx + Math.cos(st.a) * (pupilR + 3), cy + Math.sin(st.a) * (pupilR + 3));
      ctx.lineTo(cx + Math.cos(st.a) * (pupilR + 3 + (75 - pupilR) * st.l), cy + Math.sin(st.a) * (pupilR + 3 + (75 - pupilR) * st.l));
      ctx.stroke();
    }
    ctx.globalAlpha = 1;
    ctx.beginPath(); ctx.arc(cx, cy, 77, 0, Math.PI * 2); ctx.strokeStyle = 'rgba(10,8,6,.55)'; ctx.lineWidth = 5; ctx.stroke();
    ctx.beginPath(); ctx.arc(cx, cy, pupilR, 0, Math.PI * 2); ctx.fillStyle = '#050606'; ctx.fill();
    ctx.restore();
    if (!streak) { this.lids(ctx, dim); return; }

    // 光條(打在臉上)
    const px = cx + ux * s * Rb, py = cy + uy * s * Rb;
    ctx.save();
    ctx.beginPath(); ctx.ellipse(cx, cy, 190, 92, 0, 0, Math.PI * 2); ctx.clip();
    ctx.translate(px, py); ctx.rotate(-th);
    const band = ctx.createLinearGradient(0, -14, 0, 14);
    band.addColorStop(0, 'rgba(255,238,190,0)'); band.addColorStop(0.5, 'rgba(255,238,190,0.62)'); band.addColorStop(1, 'rgba(255,238,190,0)');
    ctx.fillStyle = band; ctx.fillRect(-420, -14, 840, 28);
    ctx.restore();

    // 瞳孔內的反射光
    ctx.save();
    ctx.beginPath(); ctx.arc(cx, cy, pupilR, 0, Math.PI * 2); ctx.clip();
    const dist = Math.abs(s * Rb);
    const visible = Math.max(0, Math.min(1, 1.9 - dist / pupilR)); // 光條靠近瞳孔時才出現
    if (visible > 0) {
      const col = (a) => `rgba(255,${Math.round(90 + 70 * info.brightness)},${Math.round(40 + 30 * info.brightness)},${a * contrast})`;
      if (info.neutral) {
        ctx.fillStyle = col(0.95 * visible);
        ctx.fillRect(cx - pupilR, cy - pupilR, pupilR * 2, pupilR * 2);
      } else {
        const sgn = info.motion === 'with' ? 1 : -1;
        const c = sgn * (s * Rb / pupilR) * Math.min(1.5, info.speed) * 0.9; // 瞳孔座標(以瞳孔半徑為單位)
        ctx.translate(cx + ux * c * pupilR, cy + uy * c * pupilR);
        ctx.rotate(-(th + rad(info.skew)));
        const w = info.width * pupilR * 2;
        const gr = ctx.createLinearGradient(0, -w / 2, 0, w / 2);
        gr.addColorStop(0, col(0)); gr.addColorStop(0.5, col(0.95 * visible * info.brightness + 0.1)); gr.addColorStop(1, col(0));
        ctx.fillStyle = gr; ctx.fillRect(-200, -w / 2, 400, w);
      }
    }
    ctx.restore();
    // 角膜反光:光條在角膜上的小亮點,位置跟著光條稍微移動
    if (dist < Rb * 0.9) {
      ctx.save();
      ctx.translate(cx + ux * s * 9 - 6, cy + uy * s * 9 - 8); ctx.rotate(-th);
      ctx.fillStyle = `rgba(255,252,240,${0.85 * (1 - dist / (Rb * 0.9))})`;
      ctx.beginPath(); ctx.ellipse(0, 0, 6, 2.2, 0, 0, Math.PI * 2); ctx.fill();
      ctx.restore();
    }
    this.lids(ctx, dim);
    if (!overlay) return;

    // 方向提示(字放大,手機縮小後仍讀得到)
    ctx.save();
    ctx.strokeStyle = 'rgba(255,255,255,.4)'; ctx.fillStyle = 'rgba(255,255,255,.7)'; ctx.lineWidth = 2;
    ctx.font = '600 17px sans-serif';
    ctx.beginPath(); ctx.moveTo(34, H - 36); ctx.lineTo(34 + ux * 38, H - 36 + uy * 38); ctx.stroke();
    ctx.beginPath(); ctx.arc(34 + ux * 38, H - 36 + uy * 38, 4.5, 0, Math.PI * 2); ctx.fill();
    ctx.fillText(`光條 ${Math.round(this.angle)}° · 掃動 ${Math.round(info.sweepDir)}°`, 86, H - 28);
    ctx.textAlign = 'right';
    ctx.fillText(`${eye === 'OD' ? '右眼 OD' : '左眼 OS'} · ${g.wdCm} cm`, W - 14, 26);
    ctx.textAlign = 'left';
    if (this.hintOn) {
      // 殘餘度數只在上帝視角顯示,平常只提示動向,要自己判斷還差多少
      const txt = `${info.neutral ? '中和' : info.motion === 'with' ? '順動 → 加正' : '逆動 → 減正'}${g.god ? `  (${info.r > 0 ? '+' : ''}${info.r.toFixed(2)}D)` : ''}`;
      ctx.font = '700 19px sans-serif';
      const tw = ctx.measureText(txt).width + 24;
      ctx.fillStyle = 'rgba(0,0,0,.55)';
      ctx.beginPath(); if (ctx.roundRect) ctx.roundRect(10, 8, tw, 34, 17); else ctx.rect(10, 8, tw, 34); ctx.fill();
      ctx.fillStyle = info.neutral ? '#86e0a4' : info.motion === 'with' ? '#ffcf70' : '#8fd0ff';
      ctx.fillText(txt, 22, 32);
    }
    ctx.restore();
  }

  // 眨眼:上眼瞼蓋下來(下眼瞼稍微往上)
  lids(ctx, dim) {
    const b = this.blink;
    if (!b) return;
    const cx = W / 2, cy = H / 2;
    ctx.save();
    ctx.beginPath(); ctx.ellipse(cx, cy, 192, 94, 0, 0, Math.PI * 2); ctx.clip();
    // 眼瞼邊緣是弧形:兩端固定在眼角,中間往下(上眼瞼)/往上(下眼瞼)
    const yUp = cy - 94 + 188 * b * 0.82, yLo = cy + 94 - 188 * b * 0.18;
    const L = cx - 200, R = cx + 200;
    ctx.fillStyle = dim ? '#33241f' : '#6f554a';
    ctx.beginPath(); ctx.moveTo(L, 0); ctx.lineTo(L, cy); ctx.quadraticCurveTo(cx, 2 * yUp - cy, R, cy); ctx.lineTo(R, 0); ctx.closePath(); ctx.fill();
    ctx.beginPath(); ctx.moveTo(L, H); ctx.lineTo(L, cy); ctx.quadraticCurveTo(cx, 2 * yLo - cy, R, cy); ctx.lineTo(R, H); ctx.closePath(); ctx.fill();
    ctx.strokeStyle = 'rgba(0,0,0,.65)'; ctx.lineWidth = 3;
    ctx.beginPath(); ctx.moveTo(L, cy); ctx.quadraticCurveTo(cx, 2 * yUp - cy, R, cy); ctx.stroke();
    ctx.restore();
  }
}
