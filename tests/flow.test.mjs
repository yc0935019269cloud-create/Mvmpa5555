// 以模範學生機器人跑完整流程,檢查計分與流程
import assert from 'node:assert/strict';
import { playPerfect } from './bot.mjs';
import { score } from '../src/game/scoring.js';
import { fmtRx } from '../src/sim/optics.js';

let fails = 0;
const rows = [];
for (let seed = 1; seed <= 24; seed++) {
  try {
    const g = playPerfect(seed, { mode: 'tutorial', difficulty: 2 });
    assert.equal(g.stepId, 'done', `seed ${seed} 沒走到最後:${g.stepId}`);
    const s = score(g);
    rows.push([seed, s.score100, ...['OD', 'OS'].map((e) => `${fmtRx(g.lens(e))} / ${fmtRx(g.trueRx(e))}`)]);
    if (s.score100 < 80) {
      fails++;
      console.log(`seed ${seed} score ${s.score100}`);
      for (const i of s.items) if (i.got < i.max) console.log('   -', i.cat, i.label, `${i.got}/${i.max}`, i.note);
    }
  } catch (e) {
    fails++;
    console.log('seed', seed, 'ERR', e.message);
  }
}
console.table(rows.map((r) => ({ seed: r[0], score: r[1], OD: r[2], OS: r[3] })));
const avg = rows.reduce((a, r) => a + r[1], 0) / rows.length;
console.log('平均分', avg.toFixed(1), ' 低於 80 分的 seed 數:', fails);
if (fails > 4 || avg < 88) process.exitCode = 1;
