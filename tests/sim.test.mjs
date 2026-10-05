// 光學模擬核心的單元測試(node tests/sim.test.mjs)
import assert from 'node:assert/strict';
import {
  toVec, fromVec, addVec, jccVec, jccRedAxis, blurStrength, logMARFromBlur, decimalFromLogMAR, fmtRx, mod180,
} from '../src/sim/optics.js';
import {
  generatePatient, lensToVec, effectiveBlur, predictDecimal, compareLenses, duochrome, clockDial, readRow,
} from '../src/sim/patient.js';
import { reflex, residualAt, fullyNeutral, WD_OPTIONS, wdDiopter } from '../src/sim/retino.js';

let passed = 0;
const t = (name, fn) => {
  try { fn(); passed++; console.log('  ✓', name); }
  catch (e) { console.error('  ✗', name, '\n   ', e.message); process.exitCode = 1; }
};
const near = (a, b, tol = 1e-6, msg = '') => assert.ok(Math.abs(a - b) <= tol, `${msg} ${a} vs ${b}`);

console.log('optics');
t('toVec/fromVec 來回一致', () => {
  for (const rx of [{ s: -3, c: -1.25, a: 30 }, { s: 1, c: -0.5, a: 180 }, { s: 0.5, c: -2, a: 95 }]) {
    const b = fromVec(toVec(rx));
    near(b.s, rx.s); near(b.c, rx.c); near(mod180(b.a), mod180(rx.a), 1e-6);
  }
});
t('JCC 兩個方位的等價球面度相同', () => {
  const L = { s: 1, c: -0.5, a: 180 };
  const v1 = addVec(toVec(L), jccVec(180));
  const v2 = addVec(toVec(L), jccVec(90));
  near(v1.M, v2.M);
});
t('JCC:P 模式紅點在軸上 = 多 -0.5DC 等效(合併後 -1.00 x180)', () => {
  const L = { s: 1, c: -0.5, a: 180 };
  const f = fromVec(addVec(toVec(L), jccVec(180)));
  near(f.c, -1.0); near(f.a, 180); near(f.s, 1.25);
});
t('視力:+1.00D 模糊 ≈ 0.3~0.4,0.25D ≤ 一行', () => {
  const base = -0.08;
  const v1 = decimalFromLogMAR(logMARFromBlur(1, base));
  assert.ok(v1 > 0.25 && v1 < 0.45, `v1=${v1}`);
  const v025 = decimalFromLogMAR(logMARFromBlur(0.25, base));
  assert.ok(v025 > 0.8, `v025=${v025}`);
});

console.log('patient');
t('同一 seed 生出同一人', () => {
  const a = generatePatient(7), b = generatePatient(7);
  assert.deepEqual(a.eyes.OD.rx, b.eyes.OD.rx);
});
t('完全矯正 → 最佳視力;過負不影響視力;過正降視力', () => {
  const p = generatePatient(11);
  const rx = p.eyes.OD.rx;
  const best = predictDecimal(p, 'OD', toVec(rx));
  const over = predictDecimal(p, 'OD', toVec({ ...rx, s: rx.s - 1 }));
  const fog = predictDecimal(p, 'OD', toVec({ ...rx, s: rx.s + 1 }));
  assert.ok(best >= 1.0, `best=${best}`);
  near(over, best, 0.05, 'over-minus');
  assert.ok(fog < 0.5, `fog=${fog}`);
});
t('鏡片比較:較佳鏡片勝出(球面)', () => {
  const p = generatePatient(5);
  p.reliability = 0.97; p.minusBias = 0;
  const rx = p.eyes.OD.rx;
  let ok = 0;
  for (let i = 0; i < 40; i++) {
    const ans = compareLenses(p, 'OD', toVec({ ...rx, s: rx.s + 0.75 }), toVec(rx));
    if (ans === 'second') ok++;
  }
  assert.ok(ok >= 36, `ok=${ok}`);
});
t('紅綠:過正→紅、過負→綠、正確→一樣', () => {
  const p = generatePatient(3);
  p.reliability = 0.97; p.colorBias = null;
  const rx = p.eyes.OD.rx;
  const cnt = { red: 0, green: 0, same: 0 };
  for (let i = 0; i < 30; i++) cnt[duochrome(p, 'OD', toVec({ ...rx, s: rx.s + 0.5 }))]++;
  assert.ok(cnt.red >= 27, JSON.stringify(cnt));
  const c2 = { red: 0, green: 0, same: 0 };
  for (let i = 0; i < 30; i++) c2[duochrome(p, 'OD', toVec({ ...rx, s: rx.s - 0.5 }))]++;
  assert.ok(c2.green >= 27, JSON.stringify(c2));
  const c3 = { red: 0, green: 0, same: 0 };
  for (let i = 0; i < 30; i++) c3[duochrome(p, 'OD', toVec(rx))]++;
  assert.ok(c3.same >= 25, JSON.stringify(c3));
});
t('JCC 軸度:偏愛的紅點方向 = 該把軸轉過去的方向', () => {
  const p = generatePatient(2);
  p.reliability = 0.97;
  const eye = p.eyes.OD; eye.rx = { s: -2, c: -1.5, a: 90 };
  // 試鏡軸放在 80°,真實軸 90° → 應往 + 方向(紅點在 axis+45)轉
  const trial = { s: -2, c: -1.5, a: 80 };
  const v1 = addVec(toVec(trial), jccVec(jccRedAxis('A', 1, trial.a)));
  const v2 = addVec(toVec(trial), jccVec(jccRedAxis('A', 2, trial.a)));
  const b1 = effectiveBlur(p, 'OD', v1).B, b2 = effectiveBlur(p, 'OD', v2).B;
  assert.ok(b1 < b2, `pos1 (red@a+45) should be clearer: ${b1} vs ${b2}`);
  // 反向
  const trial2 = { s: -2, c: -1.5, a: 100 };
  const w1 = addVec(toVec(trial2), jccVec(jccRedAxis('A', 1, trial2.a)));
  const w2 = addVec(toVec(trial2), jccVec(jccRedAxis('A', 2, trial2.a)));
  assert.ok(effectiveBlur(p, 'OD', w2).B < effectiveBlur(p, 'OD', w1).B);
});
t('JCC 度數:真實散光較大 → 紅點在軸上較清楚(多一片 -0.25)', () => {
  const p = generatePatient(2);
  p.eyes.OD.rx = { s: -2, c: -1.5, a: 90 };
  const trial = { s: -1.875, c: -1.0, a: 90 };
  const vP1 = addVec(toVec(trial), jccVec(jccRedAxis('P', 1, trial.a)));
  const vP2 = addVec(toVec(trial), jccVec(jccRedAxis('P', 2, trial.a)));
  assert.ok(effectiveBlur(p, 'OD', vP1).B < effectiveBlur(p, 'OD', vP2).B);
});
t('鐘面圖:霧視後給出 30 法則的軸;未霧視全部一樣', () => {
  const p = generatePatient(4);
  p.eyes.OD.rx = { s: -1, c: -1.5, a: 90 };
  const fogged = toVec({ s: 0.0, c: 0, a: 180 }); // -1 → 0 : 霧視 +1.00
  const r = clockDial(p, 'OD', fogged);
  assert.ok(!r.uniform);
  assert.deepEqual(r.lines, [3]);
  const unfog = toVec({ s: -1.75, c: 0, a: 180 });
  assert.ok(clockDial(p, 'OD', unfog).uniform);
});
t('讀視標:看得到的列全對、太小的列讀不到', () => {
  const p = generatePatient(9);
  p.reliability = 1;
  assert.equal(readRow(p, -0.1, 0.6).correct, readRow(p, -0.1, 0.6).total);
  assert.equal(readRow(p, 0.5, 1.0).correct, 0);
});

console.log('retino');
t('順加逆減:鏡片不足為順動、過量為逆動', () => {
  const trueRx = { s: -3, c: 0, a: 180 };
  const wd = 1.5;
  const neutralLens = { s: -1.5, c: 0, a: 180 };
  assert.equal(reflex(trueRx, neutralLens, wd, 90).motion, 'neutral');
  assert.equal(reflex(trueRx, { s: -2.5, c: 0, a: 180 }, wd, 90).motion, 'with');
  assert.equal(reflex(trueRx, { s: 0, c: 0, a: 180 }, wd, 90).motion, 'against');
});
t('完整中和 = 真實處方 + 工作距離', () => {
  const trueRx = { s: -2, c: -1.5, a: 170 };
  const wd = 1.5;
  const lens = { s: trueRx.s + wd, c: trueRx.c, a: trueRx.a };
  assert.ok(fullyNeutral(trueRx, lens, wd));
  assert.ok(!fullyNeutral(trueRx, { ...lens, c: -1 }, wd));
});
t('散光未矯正時,光條歪斜;對齊主軸時歪斜為 0', () => {
  const trueRx = { s: 0, c: -2, a: 180 };
  const lens = { s: 1.5, c: 0, a: 180 };
  const off = reflex(trueRx, lens, 1.5, 135);
  const on = reflex(trueRx, lens, 1.5, 180);
  assert.ok(Math.abs(off.skew) > 10, `off=${off.skew}`);
  near(on.skew, 0, 1e-6);
});

t('各工作距離:中和鏡片 = 真實處方 + 1/WD,且選項度數與距離一致', () => {
  const trueRx = { s: -2, c: -1, a: 180 };
  for (const o of WD_OPTIONS) {
    near(wdDiopter(o.cm), o.d, 1e-9, `${o.cm}cm`);
    const lens = { s: trueRx.s + o.d, c: trueRx.c, a: trueRx.a };
    assert.ok(fullyNeutral(trueRx, lens, o.d), `${o.cm} cm`);
    assert.ok(!fullyNeutral(trueRx, { ...lens, s: lens.s + 0.75 }, o.d));
  }
});

console.log(`\n${passed} passed`);
