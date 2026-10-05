// 檢影小遊戲畫面:暗室裡一條光帶掃過眼睛,瞳孔內的反射光會「順動 / 逆動 / 中和」
import { reflex } from '../sim/retino.js';
import { rad } from '../sim/optics.js';

const W = 520, H = 330;

export class RetinoView {
  constructor(canvas, game) {
    this.cv = canvas;
    canvas.width = W; canvas.height = H;
    this.ctx = canvas.getContext('2d');
    this.game = game;
    this.eye = 'OD';
    this.angle = 90; // 光條線的方向(度)
    this.auto = true;
    this.slow = false;
    this.hintOn = game.mode !== 'exam'; // 顯示「順動/逆動」判讀(教學用)
    this.manualS = null;
    this.t = 0;
    this.last = performance.now();
    this.info = null;
    this.running = false;
    this.iris = ['#6b4a2f', '#4f7a8a', '#5d6f3f', '#7a5a3a'][game.patient.seed % 4];
    this.bindPointer();
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

  draw() {
    const g = this.game, ctx = this.ctx;
    const trueRx = g.trueRx(this.eye);
    const info = (this.info = reflex(trueRx, g.lens(this.eye), g.wdD, this.angle));
    const cx = W / 2, cy = H / 2;
    const s = this.manualS !== null ? this.manualS : Math.sin(this.t * 2 * Math.PI * 0.5) * 1.05;
    const phi = rad(info.sweepDir);
    const th = rad(this.angle);
    const ux = Math.cos(phi), uy = -Math.sin(phi);
    const Rb = 125;
    const pupilR = 38;

    ctx.fillStyle = '#080b0d'; ctx.fillRect(0, 0, W, H);
    // 眼瞼 / 眼白
    ctx.save();
    const grd = ctx.createRadialGradient(cx, cy, 20, cx, cy, 190);
    grd.addColorStop(0, '#3a2a24'); grd.addColorStop(1, '#161110');
    ctx.fillStyle = grd; ctx.fillRect(0, 0, W, H);
    ctx.beginPath(); ctx.ellipse(cx, cy, 190, 92, 0, 0, Math.PI * 2);
    ctx.fillStyle = '#4a403d'; ctx.fill();
    // 虹膜與瞳孔
    ctx.beginPath(); ctx.arc(cx, cy, 78, 0, Math.PI * 2); ctx.fillStyle = this.iris; ctx.globalAlpha = 0.55; ctx.fill(); ctx.globalAlpha = 1;
    ctx.beginPath(); ctx.arc(cx, cy, pupilR, 0, Math.PI * 2); ctx.fillStyle = '#050606'; ctx.fill();
    ctx.restore();

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
      const col = (a) => `rgba(255,${Math.round(90 + 70 * info.brightness)},${Math.round(40 + 30 * info.brightness)},${a})`;
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

    // 方向提示(字放大,手機縮小後仍讀得到)
    ctx.save();
    ctx.strokeStyle = 'rgba(255,255,255,.4)'; ctx.fillStyle = 'rgba(255,255,255,.7)'; ctx.lineWidth = 2;
    ctx.font = '600 17px sans-serif';
    ctx.beginPath(); ctx.moveTo(34, H - 36); ctx.lineTo(34 + ux * 38, H - 36 + uy * 38); ctx.stroke();
    ctx.beginPath(); ctx.arc(34 + ux * 38, H - 36 + uy * 38, 4.5, 0, Math.PI * 2); ctx.fill();
    ctx.fillText(`光條 ${Math.round(this.angle)}° · 掃動 ${Math.round(info.sweepDir)}°`, 86, H - 28);
    ctx.textAlign = 'right';
    ctx.fillText(`${this.eye === 'OD' ? '右眼 OD' : '左眼 OS'} · ${g.wdCm} cm`, W - 14, 26);
    ctx.textAlign = 'left';
    if (this.hintOn) {
      const txt = `${info.neutral ? '中和' : info.motion === 'with' ? '順動 → 加正' : '逆動 → 減正'}  (${info.r > 0 ? '+' : ''}${info.r.toFixed(2)}D)`;
      ctx.font = '700 19px sans-serif';
      const tw = ctx.measureText(txt).width + 24;
      ctx.fillStyle = 'rgba(0,0,0,.55)';
      ctx.beginPath(); if (ctx.roundRect) ctx.roundRect(10, 8, tw, 34, 17); else ctx.rect(10, 8, tw, 34); ctx.fill();
      ctx.fillStyle = info.neutral ? '#86e0a4' : info.motion === 'with' ? '#ffcf70' : '#8fd0ff';
      ctx.fillText(txt, 22, 32);
    }
    ctx.restore();
  }
}
