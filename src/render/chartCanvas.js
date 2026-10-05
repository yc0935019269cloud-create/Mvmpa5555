// 視力表 / 視標畫面(2D canvas):同一份畫面貼到 3D 螢幕、視標預覽、以及「受測者視野」
import { VA_ROWS, snellen } from '../sim/optics.js';
import { rowCount } from '../sim/patient.js';
import { makeRng } from '../sim/rng.js';

export const CHART_W = 640;
export const CHART_H = 900;

const RED = '#c8372d';
const GREEN = '#1f9a55';
const FONT = '"Arial Black","Helvetica Neue",Arial,sans-serif';

export function digitsFor(seed, row) {
  const r = makeRng(Math.round(row * 100) * 131 + seed);
  return Array.from({ length: rowCount(row) }, () => r.int(0, 9));
}

function drawDigitRow(ctx, cx, cy, h, digits, color = '#111') {
  ctx.fillStyle = color;
  ctx.font = `900 ${h * 1.28}px ${FONT}`;
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  const gap = h * 1.08;
  const x0 = cx - ((digits.length - 1) * gap) / 2;
  digits.forEach((d, i) => ctx.fillText(String(d), x0 + i * gap, cy + h * 0.06));
}

// 以 1.0 列字高 = 24px 為基準(大小 ∝ 1/VA)
const baseH = (row) => 24 / row;

export function drawChart(ctx, chart, seed, { W = CHART_W, H = CHART_H, patientView = null } = {}) {
  ctx.save();
  ctx.fillStyle = '#fbfbf7';
  ctx.fillRect(0, 0, W, H);
  const cx = W / 2;
  const mode = chart.mode;

  if (mode === 'big_e') {
    ctx.fillStyle = RED; ctx.fillRect(0, 0, W / 2, H);
    ctx.fillStyle = GREEN; ctx.fillRect(W / 2, 0, W / 2, H);
    ctx.fillStyle = '#0a0a0a';
    ctx.font = `900 ${H * 0.5}px ${FONT}`;
    ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
    ctx.fillText('E', cx, H / 2);
    ctx.font = `700 22px sans-serif`;
    ctx.fillStyle = '#fff';
    ctx.fillText('20/400', cx, H - 26);
  } else if (mode === 'digits' || mode === 'rg') {
    if (mode === 'rg') {
      ctx.fillStyle = RED; ctx.fillRect(0, 0, W / 2, H);
      ctx.fillStyle = GREEN; ctx.fillRect(W / 2, 0, W / 2, H);
    }
    const rows = chart.isolate ? [chart.row] : VA_ROWS;
    // 版面:總高度分配
    const sumH = rows.reduce((s, r) => s + baseH(r) * 1.28 + 14, 0);
    const scale = chart.isolate ? Math.min(1.9, (H * 0.6) / (baseH(chart.row) * 1.28)) : (H - 60) / sumH;
    let y = chart.isolate ? H / 2 : 30;
    for (const r of rows) {
      const h = baseH(r) * scale;
      const rowH = h * 1.28 + 14 * scale;
      const cy = chart.isolate ? y : y + rowH / 2;
      // 在紅綠視標上整列加黑字
      drawDigitRow(ctx, cx, cy, h, digitsFor(seed, r), mode === 'rg' ? '#050505' : '#111');
      if (!chart.isolate) {
        ctx.font = '600 13px sans-serif';
        ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
        ctx.fillStyle = mode === 'rg' ? 'rgba(255,255,255,.85)' : '#6a757c';
        ctx.fillText(`${r.toFixed(1)}`, 10, cy);
        ctx.textAlign = 'right';
        ctx.fillText(snellen(r), W - 10, cy);
        if (r === chart.row) {
          ctx.fillStyle = '#1e7a96';
          ctx.beginPath(); ctx.moveTo(44, cy - 9); ctx.lineTo(60, cy); ctx.lineTo(44, cy + 9); ctx.fill();
        }
        y += rowH;
      }
    }
    if (chart.isolate) {
      ctx.font = '600 15px sans-serif';
      ctx.fillStyle = mode === 'rg' ? 'rgba(255,255,255,.9)' : '#6a757c';
      ctx.textAlign = 'center';
      ctx.fillText(`${chart.row.toFixed(1)}(${snellen(chart.row)})`, cx, H - 24);
    }
  } else if (mode === 'honey') {
    ctx.fillStyle = '#eceae2'; ctx.fillRect(0, 0, W, H);
    const r = Math.max(5, Math.min(34, 13 / chart.row));
    const dx = r * 3.2, dy = r * 2.8;
    ctx.fillStyle = '#151515';
    for (let j = -1, y = 40; y < H - 40; j++, y += dy) {
      for (let x = ((j & 1) ? dx / 2 : 0) + 40; x < W - 30; x += dx) {
        ctx.beginPath(); ctx.arc(x, y, r, 0, Math.PI * 2); ctx.fill();
      }
    }
    ctx.font = '600 14px sans-serif'; ctx.fillStyle = '#51606a'; ctx.textAlign = 'center';
    ctx.fillText('蜂巢視標 (JCC)', cx, H - 14);
  } else if (mode === 'clock') {
    const R = Math.min(W, H) * 0.42;
    const cyy = H / 2;
    ctx.strokeStyle = '#111'; ctx.lineCap = 'round';
    for (let n = 1; n <= 12; n++) {
      const ang = ((n * 30 - 90) * Math.PI) / 180; // 12 點在上方
      const dxu = Math.cos(ang), dyu = Math.sin(ang);
      const px = -dyu, py = dxu;
      for (const o of [-10, 0, 10]) {
        let alpha = 1;
        if (patientView?.clockDarkness) alpha = patientView.clockDarkness(n);
        ctx.globalAlpha = alpha;
        ctx.lineWidth = o === 0 ? 5 : 4;
        ctx.beginPath();
        ctx.moveTo(cx + dxu * 56 + px * o, cyy + dyu * 56 + py * o);
        ctx.lineTo(cx + dxu * R + px * o, cyy + dyu * R + py * o);
        ctx.stroke();
      }
      ctx.globalAlpha = 1;
      ctx.fillStyle = '#111';
      ctx.font = `800 ${n === 12 ? 26 : 26}px sans-serif`;
      ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
      ctx.fillText(String(n), cx + dxu * (R + 30), cyy + dyu * (R + 30));
    }
    ctx.beginPath(); ctx.arc(cx, cyy, 7, 0, Math.PI * 2); ctx.fillStyle = '#111'; ctx.fill();
  }
  ctx.restore();
}

// 模擬屈光不正造成的模糊:以多次位移疊加近似(跨瀏覽器,不依賴 ctx.filter)
// m:有效球面殘餘(D), j0/j45:殘餘散光向量, kPx:每 D 的模糊半徑(px)
export function drawBlurred(dst, src, { m, j0, j45, kPx = 16 }) {
  const ctx = dst.getContext('2d');
  const W = dst.width, H = dst.height;
  ctx.save();
  ctx.clearRect(0, 0, W, H);
  // 黃金角螺旋取樣:每個方向的模糊半徑 ∝ 該子午線的殘餘屈光力(散光 → 橢圓形模糊)
  const N = 72;
  const maxR = 26;
  const offs = [];
  for (let i = 0; i < N; i++) {
    const r = Math.sqrt((i + 0.5) / N);
    const th = i * 2.399963;
    const p = m + j0 * Math.cos(2 * th) + j45 * Math.sin(2 * th);
    const R = Math.min(maxR, Math.abs(p) * kPx);
    offs.push([Math.cos(th) * r * R, -Math.sin(th) * r * R]);
  }
  // 逐次平均:第 k 張以 alpha=1/(k+1) 疊上去 → 等權重平均
  offs.forEach(([ox, oy], k) => {
    ctx.globalAlpha = 1 / (k + 1);
    ctx.drawImage(src, ox, oy, W, H);
  });
  ctx.restore();
}
