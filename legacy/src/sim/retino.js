// 檢影模型:給定鏡片與光條角度,算出眼底反射的「型態」
import { toVec, fromVec, subVec, meridianPower, rad, mod180 } from './optics.js';

// 常見檢影工作距離(cm → 工作距離度數 D);距離越短度數越大,進工作距離時別忘了要扣掉
export const WD_OPTIONS = [
  { cm: 25, d: 4.0 },
  { cm: 33, d: 3.0 },
  { cm: 40, d: 2.5 },
  { cm: 50, d: 2.0 },
  { cm: 57, d: 1.75 },
  { cm: 67, d: 1.5, note: '常用' },
  { cm: 80, d: 1.25 },
  { cm: 100, d: 1.0 },
];
export const wdDiopter = (cm) => Math.round((100 / cm) / 0.25) * 0.25; // 67 cm → 1.50D

// 某子午線上:鏡片 − (真實處方 + 工作距離) = 殘餘。<0 順動、>0 逆動
export function residualAt(trueRx, lens, wdD, phi) {
  return meridianPower(lens, phi) - (meridianPower(trueRx, phi) + wdD);
}

// streakAngle:光條線的方向(度)。掃動方向 = streakAngle + 90,量測的是該子午線的屈光力。
export function reflex(trueRx, lens, wdD, streakAngle) {
  const sweepDir = mod180(streakAngle + 90);
  const r = residualAt(trueRx, lens, wdD, sweepDir);
  // 殘餘散光向量 → 反射光帶的偏斜(break)
  const resV = subVec(toVec(lens), { M: toVec(trueRx).M + wdD, J0: toVec(trueRx).J0, J45: toVec(trueRx).J45 });
  const resX = fromVec(resV);
  const J = Math.abs(resX.c) / 2;
  let skew = 0;
  if (J > 0.12) {
    // 光條與殘餘主軸不對齊時,反射帶會歪斜;對齊時 skew=0
    skew = 38 * Math.min(1, J / 0.5) * Math.sin(2 * rad(resX.a - streakAngle));
  }
  const neutral = Math.abs(r) < 0.125;
  return {
    r, // 該子午線殘餘屈光力
    motion: neutral ? 'neutral' : r < 0 ? 'with' : 'against', // with=順動 against=逆動
    neutral,
    skew, // 反射帶相對光條的歪斜角(度)
    sweepDir,
    brightness: neutral ? 1 : Math.max(0.5, 1 / (1 + Math.abs(r) * 1.2)),
    width: neutral ? 1 : Math.max(0.22, 0.75 / (1 + Math.abs(r) * 1.8)), // 相對瞳孔直徑
    speed: neutral ? 0 : Math.min(1.6, 0.35 / Math.max(0.2, Math.abs(r)) + 0.35),
  };
}

// 兩條主子午線都中和?(任意方向殘餘都 < 0.125)
export function fullyNeutral(trueRx, lens, wdD, tol = 0.125) {
  for (let phi = 0; phi < 180; phi += 5) {
    if (Math.abs(residualAt(trueRx, lens, wdD, phi)) >= tol) return false;
  }
  return true;
}
