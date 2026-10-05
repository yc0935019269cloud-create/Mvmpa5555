// 受測者模型:真實屈光 + 會說話(答案帶有誤差與偏好)
import { makeRng } from './rng.js';
import {
  toVec, fromVec, addVec, subVec, jccVec, jccRedAxis, blurStrength,
  logMARFromBlur, decimalFromLogMAR, VA_ROWS, mod180, r25,
} from './optics.js';

const NAMES = ['小林', '小芸', '阿豪', '怡君', '品妤', '承恩', '郁婷', '柏翰', '雅筑', '子睿', '詠晴', '冠廷', '佩珊', '宇軒', '思穎'];

// 每一排的字數(越大的字越少)
export const rowCount = (d) => (d <= 0.1 ? 1 : d <= 0.2 ? 2 : d <= 0.3 ? 3 : d <= 0.5 ? 4 : 5);

function makeEye(rng, difficulty, kind, prev) {
  let s;
  if (kind === 'myope') s = -r25(rng.range(0.75, difficulty >= 3 ? 8 : 6));
  else if (kind === 'hyperope') s = r25(rng.range(0.5, difficulty >= 3 ? 4 : 3));
  else s = r25(rng.range(-0.25, 0.5));

  // 散光:沒有 / 小(<0.75) / 中大(>=0.75)
  const roll = rng();
  let c = 0;
  const noneP = difficulty === 1 ? 0.45 : 0.2;
  const smallP = 0.25;
  if (roll < noneP) c = 0;
  else if (roll < noneP + smallP) c = -rng.pick([0.25, 0.5]);
  else c = -r25(rng.range(0.75, difficulty >= 3 ? 3 : 2.25));
  let a = 180;
  if (c !== 0) {
    const g = rng();
    const base = g < 0.5 ? 180 : g < 0.7 ? 90 : rng.pick([45, 135]);
    a = mod180(base + Math.round(rng.range(-15, 15) / 5) * 5) || 180;
  }
  // 雙眼接近:第二眼參考第一眼(輕微不等視)
  if (prev) {
    s = r25(prev.s + rng.range(-0.75, 0.75));
    if (kind === 'myope' && s > -0.25) s = -0.25;
    if (prev.c !== 0 && rng.chance(0.7)) {
      c = Math.min(0, r25(prev.c + rng.range(-0.5, 0.5)));
      a = mod180(prev.a + Math.round(rng.range(-10, 10) / 5) * 5) || 180;
    }
  }
  return { rx: { s, c, a } };
}

export function generatePatient(seed = 1, opts = {}) {
  const difficulty = opts.difficulty ?? 2;
  const rng = makeRng(seed);
  const age = rng.int(19, 32);
  const kinds = ['myope', 'myope', 'myope', 'hyperope', 'emmetrope'];
  const kind = rng.pick(kinds);
  const od = makeEye(rng, difficulty, kind, null);
  const os = makeEye(rng, difficulty, kind, od.rx);
  const eyes = { OD: od, OS: os };

  // 最佳矯正視力(logMAR):大多 1.0~1.2,少數弱視/病理
  for (const k of ['OD', 'OS']) eyes[k].base = -0.05 - rng.range(0, 0.07);
  let amblyopic = null;
  if (difficulty >= 3 && rng.chance(0.35)) {
    amblyopic = rng.pick(['OD', 'OS']);
    eyes[amblyopic].base = 0.1 + rng.range(0, 0.1); // 約 0.6~0.8
  }

  const reliability = Math.min(0.97, Math.max(0.6, 0.93 - (difficulty - 1) * 0.07 + rng.range(-0.05, 0.05)));
  return {
    seed, difficulty, name: NAMES[seed % NAMES.length], age,
    gender: rng.chance(0.5) ? 'f' : 'm',
    pd: rng.int(58, 68),
    eyes,
    amblyopic,
    accAmp: Math.max(3, 15 - 0.25 * age) , // 年輕人調節力大
    reliability,
    minusBias: difficulty >= 2 ? rng.range(0.1, 0.5) : 0.1, // 會偏好負度數(更清晰/更黑)
    colorBias: difficulty >= 2 && rng.chance(0.3) ? 'red' : null, // 偏愛紅色
    rng,
  };
}

/* ---------------- 模糊與視力 ---------------- */

// 殘餘模糊:lens 是 power vector,rx 是受測者該眼真實處方
export function eyeResidual(patient, eye, lensV) {
  return subVec(lensV, toVec(patient.eyes[eye].rx));
}

// 考慮調節後的有效模糊(過負 → 調節補回去,視力看起來不差)
export function effectiveBlur(patient, eye, lensV, { pinhole = false, comp = 0.97 } = {}) {
  const res = eyeResidual(patient, eye, lensV);
  let m = res.M;
  if (m < 0) m += Math.min(-m, patient.accAmp) * comp;
  let B = blurStrength({ M: m, J0: res.J0, J45: res.J45 });
  if (pinhole) B *= 0.35;
  return { B, res, dM: res.M };
}

export function predictLogMAR(patient, eye, lensV, opts) {
  const { B } = effectiveBlur(patient, eye, lensV, opts);
  return logMARFromBlur(B, patient.eyes[eye].base);
}
export const predictDecimal = (p, e, l, o) => decimalFromLogMAR(predictLogMAR(p, e, l, o));

/* ---------------- 說話:讀視標 ---------------- */

// 讀某一排:回傳讀對幾個字
export function readRow(patient, logMAR, rowDecimal) {
  const rowLM = -Math.log10(rowDecimal);
  const diff = rowLM - logMAR; // >0:這排比視力閾值大,讀得到
  const total = rowCount(rowDecimal);
  let frac;
  if (diff >= 0.02) frac = 1;
  else if (diff <= -0.1) frac = 0;
  else frac = (diff + 0.1) / 0.12;
  let correct = Math.round(total * frac);
  if (patient.rng.chance((1 - patient.reliability) * 0.5) && correct > 0 && correct < total) correct += patient.rng.chance(0.5) ? 1 : -1;
  return { correct: Math.max(0, Math.min(total, correct)), total };
}

// 由 logMAR 求「最佳可讀行」與 ± 字數(給紀錄單核對用)
export function bestRow(logMAR) {
  let best = VA_ROWS[0];
  for (const d of VA_ROWS) if (-Math.log10(d) >= logMAR - 0.02) best = d;
  return best;
}

/* ---------------- 說話:比較兩個鏡片 ---------------- */

const gauss = (rng) => {
  let u = 0, v = 0;
  while (!u) u = rng();
  while (!v) v = rng();
  return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v);
};

// 比較分數:越小越清楚。過負會稍微扣分(影像縮小、字變黑一點、有點累)
function cmpScore(patient, eye, lensV, opts) {
  const { B, dM } = effectiveBlur(patient, eye, lensV, opts);
  return B + (dM < 0 ? 0.04 * -dM : 0);
}

// kind: 'va'(MPMVA 兩片比較) | 'jcc'
export function compareLenses(patient, eye, lensA, lensB, kind = 'va', opts) {
  const sA = cmpScore(patient, eye, lensA, opts);
  const sB = cmpScore(patient, eye, lensB, opts);
  let d = sB - sA + gauss(patient.rng) * (1 - patient.reliability) * (kind === 'jcc' ? 0.05 : 0.06);
  // 偏好更負的鏡片
  const mA = toVec(fromVec(lensA)).M;
  const mB = toVec(fromVec(lensB)).M;
  if (mB < mA - 0.001) d -= patient.minusBias * 0.03;
  else if (mB > mA + 0.001) d += patient.minusBias * 0.03;
  const thr = kind === 'jcc' ? 0.025 : 0.04;
  if (Math.abs(d) < thr) return 'same';
  return d > 0 ? 'first' : 'second'; // first = 鏡片1(A)比較清楚
}

/* ---------------- 說話:紅綠 ---------------- */

// 回傳 'red' | 'green' | 'same'
export function duochrome(patient, eye, lensV) {
  const { res } = effectiveBlur(patient, eye, lensV);
  const dM = res.M < 0 ? res.M * 0.4 : res.M; // 調節只部分掩蓋過負
  const J2 = res.J0 * res.J0 + res.J45 * res.J45;
  const bR = Math.max(0, Math.sqrt((dM - 0.25) ** 2 + J2) - 0.2);
  const bG = Math.max(0, Math.sqrt((dM + 0.25) ** 2 + J2) - 0.2);
  const d = bG - bR + gauss(patient.rng) * (1 - patient.reliability) * 0.05; // >0 紅較清楚
  const thr = 0.05;
  if (Math.abs(d) < thr) {
    if (patient.colorBias === 'red' && patient.rng.chance(0.4)) return 'red';
    return 'same';
  }
  return d > 0 ? 'red' : 'green';
}

/* ---------------- 說話:散光鐘面圖 ---------------- */

// 回傳 { fogOk, uniform, lines:[n,...], axis }
export function clockDial(patient, eye, lensV) {
  const res = eyeResidual(patient, eye, lensV);
  const J = Math.hypot(res.J0, res.J45);
  const fogOk = res.M >= 0.5;
  if (J < 0.2 || !fogOk) return { fogOk, uniform: true, lines: [], axis: null };
  // 還需要補上的負散光軸(= 殘餘向量的反向);最黑的線 → 數字 × 30 = 軸
  const psi = fromVec({ M: -res.M, J0: -res.J0, J45: -res.J45 }).a;
  const n = psi / 30; // 30 法則:數字 × 30 = 軸
  const frac = n - Math.floor(n);
  const norm = (k) => ((Math.round(k) - 1 + 6) % 6) + 1;
  const lines = Math.abs(frac - 0.5) < 0.17 ? [norm(Math.floor(n)), norm(Math.floor(n) + 1)] : [norm(n)];
  return { fogOk, uniform: false, lines, axis: psi };
}

/* ---------------- 工具:phoropter 該眼鏡片 → power vector ---------------- */

export function lensToVec(lens, jcc) {
  let v = toVec(lens);
  if (jcc) v = addVec(v, jccVec(jccRedAxis(jcc.mode, jcc.pos, lens.a)));
  return v;
}

/* ---------------- 說話文字 ---------------- */

const T = {
  first: ['第一個比較清楚。', '嗯…第 1 個。', '1 好像比較清楚耶。', '第一個。'],
  second: ['第二個比較清楚。', '2 比較清楚。', '嗯…第 2 個。', '第二個吧。'],
  same: ['差不多耶。', '兩個看起來一樣。', '嗯…沒什麼差別。', '一樣清楚。'],
  red: ['紅色那邊比較清楚。', '紅色的字比較清楚。', '紅的。'],
  green: ['綠色那邊比較清楚。', '綠色的字比較清楚。', '綠的。'],
  left: ['左邊那邊比較清楚。', '左邊。'],
  right: ['右邊那邊比較清楚。', '右邊。'],
};
export const say = (patient, key) => patient.rng.pick(T[key]);
