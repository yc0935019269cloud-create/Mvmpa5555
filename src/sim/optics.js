// 光學核心:用「功率向量」(M, J0, J45) 計算殘餘屈光不正、模糊量、預測視力
// 處方一律用負散光寫法:{ s, c(<=0), a(0-180, TABO) }

export const rad = (d) => (d * Math.PI) / 180;
export const mod180 = (a) => ((a % 180) + 180) % 180;
export const r25 = (x) => Math.round(x / 0.25) * 0.25;

export function toVec({ s, c, a }) {
  const half = c / 2;
  return {
    M: s + half,
    J0: -half * Math.cos(2 * rad(a)),
    J45: -half * Math.sin(2 * rad(a)),
  };
}

export function fromVec({ M, J0, J45 }) {
  const j = Math.hypot(J0, J45);
  const c = -2 * j;
  let a = j < 1e-9 ? 180 : (Math.atan2(J45, J0) * 90) / Math.PI;
  a = mod180(a);
  if (a === 0) a = 180;
  return { s: M - c / 2, c, a };
}

export const addVec = (u, v) => ({ M: u.M + v.M, J0: u.J0 + v.J0, J45: u.J45 + v.J45 });
export const subVec = (u, v) => ({ M: u.M - v.M, J0: u.J0 - v.J0, J45: u.J45 - v.J45 });

// 某一子午線上的屈光力
export function meridianPower({ s, c, a }, phi) {
  const d = Math.sin(rad(phi - a));
  return s + c * d * d;
}

// JCC (±0.25)。redAxis = 紅點所在方向(= 負圓柱軸位),純圓柱 ±0.50DC,球面等價為 0
export function jccVec(redAxis, strength = 0.25) {
  return { M: 0, J0: strength * Math.cos(2 * rad(redAxis)), J45: strength * Math.sin(2 * rad(redAxis)) };
}

// 以試鏡片軸位 A 為基準,A / P 模式、位置 1/2 的紅點方向
export function jccRedAxis(mode, pos, trialAxis) {
  if (mode === 'P') return mod180(pos === 1 ? trialAxis : trialAxis + 90);
  return mod180(pos === 1 ? trialAxis + 45 : trialAxis - 45);
}

// 模糊強度 B (Thibos):殘餘向量長度
export const blurStrength = (res) => Math.sqrt(res.M * res.M + res.J0 * res.J0 + res.J45 * res.J45);

export const VA_K = 0.5; // 每 1D 模糊 → +0.5 logMAR
export const VA_DEADZONE = 0.1; // 約 ±0.10D 內看不出差異

export function logMARFromBlur(B, base) {
  return base + Math.max(0, VA_K * (B - VA_DEADZONE));
}
export const decimalFromLogMAR = (l) => Math.pow(10, -l);
export const logMARFromDecimal = (d) => -Math.log10(d);

export const VA_ROWS = [0.1, 0.2, 0.3, 0.4, 0.5, 0.6, 0.7, 0.8, 0.9, 1.0, 1.2, 1.5];
export const snellen = (d) => {
  const den = Math.round(20 / d);
  return `20/${den}`;
};

// 格式化處方
export function fmtSph(x) {
  const v = Math.round(x * 100) / 100;
  if (Math.abs(v) < 0.001) return 'PL';
  return (v > 0 ? '+' : '−') + Math.abs(v).toFixed(2);
}
export function fmtRx({ s, c, a }) {
  const sp = fmtSph(s);
  if (Math.abs(c) < 0.001) return `${sp} DS`;
  return `${sp} −${Math.abs(c).toFixed(2)} × ${String(Math.round(a)).padStart(3, '0')}`;
}
